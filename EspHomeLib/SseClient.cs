using ChannelLib;
using EspHomeLib.Dto;
using EspHomeLib.Option;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EspHomeData _espHomeData;
    private readonly EventBroadcaster<Dictionary<string, string>, IChannelSubscriber> _channelSubscriberEcoWitt;
    private readonly EventBroadcaster<Exception, IChannelSubscriber> _channelSubscriberException;
    private readonly ILogger<SseClient> _logger;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private Uri _uri;

    private CancellationTokenSource cancellationTokenSource;

    private Task runningInstance;

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SseClient(IHttpClientFactory httpClientFactory,
                     EspHomeData espHomeData,
                     EventBroadcaster<Dictionary<string, string>, IChannelSubscriber> channelSubscriberEcoWitt,
                     EventBroadcaster<Exception, IChannelSubscriber> channelSubscriberException,
                     ILogger<SseClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _espHomeData = espHomeData;

        _espHomeData.OnEspHomeOptionChanged += OnEspHomeOptionChanged;

        _channelSubscriberEcoWitt = channelSubscriberEcoWitt;
        _channelSubscriberException = channelSubscriberException;
    }

    private void OnEspHomeOptionChanged(object? sender, EventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(SseClient));

        //do nothing for now

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(SseClient));
    }

    public void Start(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Start {uri} Start", nameof(SseClient), uri);

        if (cancellationTokenSource == null)
        {
            cancellationTokenSource = new CancellationTokenSource();
            _uri = uri;

            runningInstance = StartMonitoringAsync();
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Start {uri} End", nameof(SseClient), uri);
    }

    private async Task StartMonitoringAsync()
    {
        try
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartMonitoringAsync {uri} Start", nameof(SseClient), _uri);

            _semaphore.Wait(cancellationTokenSource.Token);

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!await PingAsync(_uri.Host))
                {
                    await Task.Delay(_espHomeData.EsphomeOptions.SseClient.PingDelay * 1000, cancellationTokenSource.Token);
                    continue;
                }

                try
                {
                    await MonitoringAsync(_uri);
                }
                catch (Exception ex)
                {
                    ex.Data.Add("source", _uri);
                    _channelSubscriberException.Broadcast(ex);

                    await Task.Delay(_espHomeData.EsphomeOptions.SseClient.PingDelay * 1000, cancellationTokenSource.Token);
                }
            }
        }
        finally
        {
            _semaphore.Release();
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartMonitoringAsync {uri} End", nameof(SseClient), _uri);
        }
    }

    private async Task<bool> PingAsync(string host)
    {
        bool result;

        try
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} PingAsync {host} Start", nameof(SseClient), host);

            using var ping = new Ping();

            var pingReply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(_espHomeData.EsphomeOptions.SseClient.PingTimeout), cancellationToken: cancellationTokenSource.Token);

            result = pingReply.Status == IPStatus.Success;
        }
        catch (Exception ex)
        {
            ex.Data.Add("source", _uri);
            _channelSubscriberException.Broadcast(ex);

            result = false;
        }
        finally
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} PingAsync {host} End", nameof(SseClient), host);
        }

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

        using var timeoutTokenSource = new CancellationTokenSource();
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_espHomeData.EsphomeOptions.SseClient.TimeoutDelay));

        using var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, timeoutTokenSource.Token);

        await foreach (var item in parser.EnumerateAsync(cancellationToken.Token))
        {
            if (timeoutTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException($"{uri} cancellationTokenTimeout");
            }

            if (string.Equals(item.EventType, "state", StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} MonitoringAsync {uri} : {data}", nameof(SseClient), uri, item.Data);

                timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_espHomeData.EsphomeOptions.SseClient.TimeoutDelay));

                var espEvent = JsonSerializer.Deserialize<EspEvent>(item.Data, jsonOptions);

                _espHomeData.UpdateData(espEvent);
            }
            else if(string.Equals(item.EventType, "weather", StringComparison.OrdinalIgnoreCase))
            {
                timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_espHomeData.EsphomeOptions.SseClient.TimeoutDelay));

                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(item.Data);

                _channelSubscriberEcoWitt.Broadcast(dict);
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
        try
        {
            _semaphore.Wait(); //wait until the cancelled is completed

            _semaphore.Dispose();
        }
        catch (ObjectDisposedException) { }

        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        _espHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose {_uri} End", nameof(SseClient), _uri);
    }
}
