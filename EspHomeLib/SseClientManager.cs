using EspHomeLib.Option;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib;
public class SseClientManager : IHostedService, IDisposable
{
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Uri, SseClient> _sseClients = new();
    private readonly ILogger<SseClientManager> _logger;
    private readonly ProcessEvent _processEvent;

    public SseClientManager(IServiceProvider serviceProvider,
                            ILogger<SseClientManager> logger,
                            ProcessEvent processEvent,
                            IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor)
    {
        _serviceProvider = serviceProvider;
        _processEvent = processEvent;
        _logger = logger;
        _esphomeOptions = esphomeOptionsMonitor.CurrentValue;

        _esphomeOptionsDispose = esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }

    private void OnOptionChanged(EsphomeOptions currentValue)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(SseClientManager));

        _esphomeOptions = currentValue;
        OnConfigChange(currentValue);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(SseClientManager));
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(SseClientManager));

        foreach (var client in _sseClients.Values)
        {
            DisposeClient(client);
        }

        _sseClients.Clear();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(SseClientManager));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if(_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync Start", nameof(SseClientManager));

        InitializeClients(_esphomeOptions.Uri);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync End", nameof(SseClientManager));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync Start", nameof(SseClientManager));

        Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync End", nameof(SseClientManager));

        return Task.CompletedTask;
    }

    private void InitializeClients(IEnumerable<Uri> uris)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitializeClients Start", nameof(SseClientManager));

        foreach (var uri in uris)
        {
            AddClient(uri);
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitializeClients End", nameof(SseClientManager));
    }

    private void OnConfigChange(EsphomeOptions newConfig)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnConfigChange Start", nameof(SseClientManager));

        var newUris = newConfig.Uri;
        var currentUris = _sseClients.Keys;

        foreach (var uri in newUris.Except(currentUris))
        {
            AddClient(uri);
        }

        foreach (var uri in currentUris.Except(newUris))
        {
            RemoveClient(uri);
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnConfigChange End", nameof(SseClientManager));
    }
    private void AddClient(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} AddClient {uri} Start", nameof(SseClientManager), uri);

        var sseClient = _serviceProvider.GetRequiredService<SseClient>();

        sseClient.OnEventReceived = _processEvent;

        _sseClients[uri] = sseClient;
        sseClient.Start(uri);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} AddClient {uri} End", nameof(SseClientManager), uri);
    }

    private void DisposeClient(SseClient client)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} DisposeClient {client} Start", nameof(SseClientManager), client);

        client.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} DisposeClient {client} End", nameof(SseClientManager), client);
    }

    private void RemoveClient(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} RemoveClient {uri} Start", nameof(SseClientManager), uri);

        if (_sseClients.TryRemove(uri, out var client))
        {
            DisposeClient(client);
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} RemoveClient {uri} End", nameof(SseClientManager), uri);
    }
}
