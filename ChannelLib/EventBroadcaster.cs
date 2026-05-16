using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ChannelLib;

public sealed class EventBroadcaster<TMessage, TClientId> : IDisposable where TClientId: notnull
{
    private int _disposed; // 0 = false, 1 = true

    private readonly ILogger _logger;

    public EventBroadcaster(ILogger<EventBroadcaster<TMessage, TClientId>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    private readonly ConcurrentDictionary<TClientId, Channel<TMessage>> _clients = new();

    public bool IsAlreadySubscribed(TClientId client) => _clients.ContainsKey(client);

    public EventSubscriber<TMessage> Subscribe(TClientId client)
    {
        CheckIfDisposed();

        //todo use IConfiguration to configure the channel
        var channel = Channel.CreateBounded<TMessage>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        if (!_clients.TryAdd(client, channel))
        {
            throw new InvalidOperationException($"Client '{client}' already subscribed.");
        }

        // Guard against racing with Dispose
        if (Volatile.Read(ref _disposed) == 1)
        {
            _clients.TryRemove(client, out _);
            channel.Writer.TryComplete();
            throw new ObjectDisposedException(nameof(EventBroadcaster<,>));
        }

        return new EventSubscriber<TMessage>(channel.Reader, () => Unsubscribe(client));
    }

    public bool Unsubscribe(TClientId client)
    {
        if (_clients.TryRemove(client, out var channel))
        {
            channel.Writer.TryComplete();
            return true;
        }

        return false;
    }

    public void BroadcastByName(string channelNameId, TMessage message)
    {
        CheckIfDisposed();

        foreach (var sub in _clients.Keys.Where(x => x is IChannelSubscriber<string> y && y.ChannelNameId == channelNameId))
        {
            if (_clients.TryGetValue(sub, out var channel))
            {
                if (!channel.Writer.TryWrite(message))
                {
                    _logger.LogInformation("Broadcast for {T} and {key} failed", typeof(TMessage), channelNameId);
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
                _logger.LogInformation("Broadcast for {T} and {key} failed", typeof(TMessage), key);
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