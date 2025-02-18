﻿using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib;
public class SseClient : IDisposable
{
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SseClient> _logger;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Uri _uri;

    private CancellationTokenSource cancellationTokenSource;

    public IProcessEvent OnEventReceived { get; set; }

    private Task runningInstance;

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };


    public SseClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor, ILogger<SseClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _esphomeOptions = esphomeOptionsMonitor.CurrentValue;

        _esphomeOptionsDispose = esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }
    private void OnOptionChanged(EsphomeOptions currentValue)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(SseClient));

        _esphomeOptions = currentValue;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(SseClient));
    }

    public void Start(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Start {uri} Start", nameof(SseClient), uri);

        if (cancellationTokenSource == null)
        {
            cancellationTokenSource = new CancellationTokenSource();
            runningInstance = StartMonitoringAsync(uri);
            _uri = uri;
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Start {uri} End", nameof(SseClient), uri);
    }

    private async Task StartMonitoringAsync(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartMonitoringAsync {uri} Start", nameof(SseClient), uri);

        try
        {
            _semaphore.Wait(cancellationTokenSource.Token);

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!await PingAsync(uri.Host))
                {
                    //OnPingFailed?.Invoke("returned false");
                    await Task.Delay(_esphomeOptions.SseClient.PingDelay * 1000, cancellationTokenSource.Token);
                    continue;
                }

                try
                {
                    await MonitoringAsync(uri);
                }
                catch (Exception ex)
                {
                    var onEventReceived = OnEventReceived;
                    if (onEventReceived != null)
                    {
                        await onEventReceived.SendAsync(ex, _uri);
                    }
                    await Task.Delay(_esphomeOptions.SseClient.PingDelay * 1000, cancellationTokenSource.Token);
                }
            }
        }
        finally
        {
            _semaphore.Release();
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartMonitoringAsync {uri} End", nameof(SseClient), uri);
        }
    }

    private async Task<bool> PingAsync(string host)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} PingAsync {host} Start", nameof(SseClient), host);

        bool result;

        try
        {
            using var ping = new Ping();

            var pingReply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(_esphomeOptions.SseClient.PingTimeout), cancellationToken: cancellationTokenSource.Token);

            result = pingReply.Status == IPStatus.Success;
        }
        catch (Exception ex)
        {
            var onEventReceived = OnEventReceived;
            if (onEventReceived != null)
            {
                await onEventReceived.SendAsync(ex, _uri);
            }
            result = false;
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} PingAsync {host} End", nameof(SseClient), host);

        return result;
    }

    private async Task MonitoringAsync(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} MonitoringAsync {uri} Start", nameof(SseClient), uri);

        using var httpClient = _httpClientFactory.CreateClient("sseClient");
        using var stream = await httpClient.GetStreamAsync(uri, cancellationTokenSource.Token);

        var parser = SseParser.Create(stream, (type, data) =>
        {
            var str = Encoding.UTF8.GetString(data);
            return str;
        });

        //todo: remove if not useful in the future
        using var timeoutTokenSource = new CancellationTokenSource();
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_esphomeOptions.SseClient.TimeoutDelay));

        using var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, timeoutTokenSource.Token);

        await foreach (var item in parser.EnumerateAsync(cancellationToken.Token))
        {
            if (timeoutTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException($"{uri} cancellationTokenTimeout");
            }

            var onEventReceivedData = OnEventReceived;
            if (onEventReceivedData != null)
            {
                await onEventReceivedData.SendAsync(item.Data, _uri);
            }

            if (string.Equals(item.EventType, "state", StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} MonitoringAsync {uri} : {data}", nameof(SseClient), uri, item.Data);

                timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_esphomeOptions.SseClient.TimeoutDelay));

                var espEvent = JsonSerializer.Deserialize<EspEvent>(item.Data, jsonOptions);

                espEvent.UnixTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                var onEventReceivedJson = OnEventReceived;
                if (onEventReceivedJson != null)
                {
                    await onEventReceivedJson.SendAsync(espEvent, _uri);
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} MonitoringAsync {uri} End", nameof(SseClient), uri);
    }

    public override string ToString()
    {
        if (_uri == null)
        {
            return base.ToString();
        }

        return $"{nameof(SseClient)} {_uri}";
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose {_uri} Start", nameof(SseClient), _uri);

        cancellationTokenSource?.Cancel();
        _semaphore.Wait(); //wait until the cancelled is completed

        _semaphore.Dispose();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        _esphomeOptionsDispose?.Dispose();
        OnEventReceived = null;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose {_uri} End", nameof(SseClient), _uri);
    }
}
