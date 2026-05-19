using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SseLib.Core.Option;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;

namespace SseLib.ChannelLib;

public sealed class EventBroadcaster<TClientId, TMessage> : IDisposable where TClientId: notnull
{
    private int _disposed; // 0 = false, 1 = true

    private readonly ILogger<EventBroadcaster<TClientId, TMessage>> _logger;
    private readonly int channelBoundSize;

    public EventBroadcaster(ILogger<EventBroadcaster<TClientId, TMessage>> logger, IOptions<EsphomeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        channelBoundSize = options.Value.SseClient.ChannelLimit;
    }

    private readonly ConcurrentDictionary<TClientId, Channel<TMessage>> _clients = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<TClientId, Channel<TMessage>>> _clientsByName = new();

    public bool IsAlreadySubscribed(TClientId client) => _clients.ContainsKey(client);

    public EventSubscriber<TMessage> Subscribe(TClientId client)
    {
        CheckIfDisposed();

        var channel = Channel.CreateBounded<TMessage>(
            new BoundedChannelOptions(channelBoundSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        if (!_clients.TryAdd(client, channel))
        {
            throw new InvalidOperationException($"Client '{client}' already subscribed.");
        }

        if (client is IChannelSubscriber sub)
        {
            _clientsByName
                .GetOrAdd(sub.ChannelNameId, _ => new ConcurrentDictionary<TClientId, Channel<TMessage>>())
                .TryAdd(client, channel);
        }

        // Guard against racing with Dispose
        if (Volatile.Read(ref _disposed) == 1)
        {
            Unsubscribe(client); 
            throw new ObjectDisposedException(nameof(EventBroadcaster<,>));
        }

        return new EventSubscriber<TMessage>(channel.Reader, () => Unsubscribe(client));
    }

    public bool Unsubscribe(TClientId client)
    {
        if (_clients.TryRemove(client, out var channel))
        {
            channel.Writer.TryComplete();

            if (client is IChannelSubscriber sub && 
                _clientsByName.TryGetValue(sub.ChannelNameId, out var namedClients))
            {
                namedClients.TryRemove(client, out _);

                if (namedClients.IsEmpty)
                {
                    _clientsByName.TryRemove(sub.ChannelNameId, out _);
                }
            }

            return true;
        }

        return false;
    }

    public void BroadcastByName(string channelNameId, TMessage message)
    {
        CheckIfDisposed();

        if (_clientsByName.TryGetValue(channelNameId, out var namedClients))
        {
            foreach (var (key, channel) in namedClients)
            {
                if (!channel.Writer.TryWrite(message))
                {
                    if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Broadcast for {T} and {key} failed", typeof(TMessage), key);
                }
            }
        }
    }

    public void Broadcast(TMessage message)
    {
        CheckIfDisposed();

        foreach (var (key, channel) in _clients)
        {
            if(!channel.Writer.TryWrite(message))
            {
                if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Broadcast for {T} and {key} failed", typeof(TMessage), key);
            }
        }
    }

    private void CheckIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            throw new ObjectDisposedException(nameof(EventBroadcaster<,>));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return; // idempotent
        }

        var clients = _clients.ToArray();
        foreach (var (key, _) in clients)
        {
            Unsubscribe(key);
        }
    }
}