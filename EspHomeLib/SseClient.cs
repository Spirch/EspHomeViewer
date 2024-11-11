using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib;
public class SseClient : IDisposable
{
    private readonly IOptionsMonitor<EsphomeOptions> _esphomeOptionsMonitor;
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SseClient> _logger;

    private readonly SemaphoreSlim _semaphore = new(1,1);
    private Uri _uri;

    private CancellationTokenSource cancellationTokenSource;

    public IProcessEvent OnEventReceived { get; set; }

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public const int DATA_START = 6; //"data: ".Length;
    public const string DATA_JSON = "data: {";
    public const string EVENT_STATE = "event: state";

    public SseClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor, ILogger<SseClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _esphomeOptionsMonitor = esphomeOptionsMonitor;
        _logger = logger;

        InitOption();

        _esphomeOptionsDispose = _esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }
    private void OnOptionChanged(EsphomeOptions _)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(SseClient));

        InitOption();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(SseClient));
    }
    private void InitOption()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption Start", nameof(SseClient));

        _esphomeOptions = _esphomeOptionsMonitor.CurrentValue;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption End", nameof(SseClient));
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose {_uri} Start", nameof(SseClient), _uri);

        Stop();
        _semaphore.Wait();
        _semaphore.Dispose();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        _esphomeOptionsDispose?.Dispose();
        OnEventReceived = null;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose {_uri} End", nameof(SseClient), _uri);
    }

    public override string ToString()
    {
        if(_uri == null)
        {
            return base.ToString();
        }

        return $"{nameof(SseClient)} {_uri}";
    }

    public void Start(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Start {uri} Start", nameof(SseClient), uri);

        if (cancellationTokenSource == null)
        {
            cancellationTokenSource = new CancellationTokenSource();
            _ = StartMonitoringAsync(uri);
            _uri = uri;
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Start {uri} End", nameof(SseClient), uri);
    }

    public void Stop()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Stop {_uri} Start", nameof(SseClient), _uri);

        cancellationTokenSource?.Cancel();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Stop {_uri} End", nameof(SseClient), _uri);
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
                    if (OnEventReceived != null)
                    {
                        await OnEventReceived.SendAsync(ex, _uri);
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
            if (OnEventReceived != null)
            {
                await OnEventReceived.SendAsync(ex, _uri);
            }
            result = false;
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} PingAsync {host} End", nameof(SseClient), host);

        return result;
    }

    private async Task MonitoringAsync(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} MonitoringAsync {uri} Start", nameof(SseClient), uri);

        bool handleNext = false;

        using var httpClient = _httpClientFactory.CreateClient();
        using var stream = await httpClient.GetStreamAsync(uri, cancellationTokenSource.Token);
        using var reader = new StreamReader(stream);

        using var timeoutTokenSource = new CancellationTokenSource();
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_esphomeOptions.SseClient.TimeoutDelay));

        using var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, timeoutTokenSource.Token);

        while (!cancellationToken.IsCancellationRequested)
        {
            string data = await reader.ReadLineAsync(cancellationToken.Token);

            if (timeoutTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException($"{uri} cancellationTokenTimeout");
            }

            if (data == null)
            {
                throw new SocketException((int)SocketError.HostDown, $"{uri} remote connection closed");
            }

            if (OnEventReceived != null)
            {
                await OnEventReceived.SendAsync(data, _uri);
            }

            if (handleNext && data.StartsWith(DATA_JSON, StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Debug))  _logger.LogDebug("{Class} MonitoringAsync {uri} : {data}", nameof(SseClient), uri, data);

                timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(_esphomeOptions.SseClient.TimeoutDelay));

                var json = JsonSerializer.Deserialize<EspEvent>(data.AsSpan(DATA_START), jsonOptions);

                json.UnixTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                if(OnEventReceived != null)
                {
                    await OnEventReceived.SendAsync(json, _uri);
                }
            }

            handleNext = string.Equals(data, EVENT_STATE, StringComparison.OrdinalIgnoreCase);
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} MonitoringAsync {uri} End", nameof(SseClient), uri);
    }
}
