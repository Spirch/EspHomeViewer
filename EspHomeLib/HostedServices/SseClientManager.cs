using EspHomeLib.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib.HostedServices;
public class SseClientManager : IHostedService, IDisposable
{
    private readonly EspHomeData _espHomeData;

    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Uri, SseClient> _sseClients = new();
    private readonly ILogger<SseClientManager> _logger;

    public SseClientManager(EspHomeData espHomeData,
                            IServiceProvider serviceProvider,
                            ILogger<SseClientManager> logger)
    {
        _espHomeData = espHomeData;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _espHomeData.OnEspHomeOptionChanged += OnEspHomeOptionChanged;
    }

    private void OnEspHomeOptionChanged(object? sender, EventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(SseClientManager));

        var newUris = _espHomeData.EsphomeOptions.Uri;
        var currentUris = _sseClients.Keys;

        foreach (var uri in newUris.Except(currentUris))
        {
            AddClient(uri);
        }

        foreach (var uri in currentUris.Except(newUris))
        {
            RemoveClient(uri);
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(SseClientManager));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync Start", nameof(SseClientManager));

        InitializeClients(_espHomeData.EsphomeOptions.Uri);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync End", nameof(SseClientManager));

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

    private void AddClient(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} AddClient {uri} Start", nameof(SseClientManager), uri);

        var sseClient = _serviceProvider.GetRequiredService<SseClient>();

        if (_sseClients.TryAdd(uri, sseClient))
        {
            _sseClients[uri] = sseClient;
            sseClient.Start(uri);
        }
        else
        {
            sseClient.Dispose(); // don't need the new one
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} AddClient {uri} End", nameof(SseClientManager), uri);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync Start", nameof(SseClientManager));

        Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync End", nameof(SseClientManager));

        return Task.CompletedTask;
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

    private void DisposeClient(SseClient client)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} DisposeClient {client} Start", nameof(SseClientManager), client);

        client.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} DisposeClient {client} End", nameof(SseClientManager), client);
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(SseClientManager));

        _espHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;

        foreach (var client in _sseClients.Values)
        {
            DisposeClient(client);
        }

        _sseClients.Clear();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(SseClientManager));
    }
}
