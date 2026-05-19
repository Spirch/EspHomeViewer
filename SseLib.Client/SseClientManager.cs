using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SseLib.Core.Dto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SseLib.Client;
public sealed class SseClientManager : IHostedService, IAsyncDisposable
{
    private int _disposed; // 0 = false, 1 = true

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

    private async void OnEspHomeOptionChanged(object? sender, EventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(SseClientManager));

        try
        {
            var newUris = _espHomeData.EsphomeOptions.Uri;
            var currentUris = _sseClients.Keys;

            foreach (var uri in newUris.Except(currentUris))
            {
                await AddClientAsync(uri);
            }

            foreach (var uri in currentUris.Except(newUris))
            {
                await RemoveClientAsync(uri);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Class} OnEspHomeOptionChanged Exception", nameof(SseClientManager));
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(SseClientManager));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync Start", nameof(SseClientManager));

        await InitializeClientsAsync(_espHomeData.EsphomeOptions.Uri);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync End", nameof(SseClientManager));
    }

    private async Task InitializeClientsAsync(IEnumerable<Uri> uris)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitializeClients Start", nameof(SseClientManager));

        foreach (var uri in uris)
        {
           await AddClientAsync(uri);
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitializeClients End", nameof(SseClientManager));
    }

    private async Task AddClientAsync(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} AddClient {uri} Start", nameof(SseClientManager), uri);

        bool success = false;
        var sseClient = _serviceProvider.GetRequiredService<SseClient>();

        if (_sseClients.TryAdd(uri, sseClient))
        {
            success = sseClient.Start(uri);
        }

        if(!success)
        {
            await sseClient.DisposeAsync();
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} AddClient {uri} End", nameof(SseClientManager), uri);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync Start", nameof(SseClientManager));

        await DisposeAsync();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync End", nameof(SseClientManager));
    }

    private async Task RemoveClientAsync(Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} RemoveClient {uri} Start", nameof(SseClientManager), uri);

        if (_sseClients.TryRemove(uri, out var client))
        {
            await client.DisposeAsync();
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} RemoveClient {uri} End", nameof(SseClientManager), uri);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return; // idempotent
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(SseClientManager));

        _espHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;

        foreach (var client in _sseClients.Values)
        {
            await client.DisposeAsync();
        }

        _sseClients.Clear();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(SseClientManager));
    }
}
