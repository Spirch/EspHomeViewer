using System;
using System.Threading;
using System.Threading.Channels;

namespace SseLib.ChannelLib;

public sealed class EventSubscriber<T> : IDisposable
{
    public ChannelReader<T> Reader { get; }
    private readonly Action _unsubscribe;
    private int _disposed; // 0 = false, 1 = true

    public EventSubscriber(ChannelReader<T> reader, Action unsubscribe)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(unsubscribe);

        Reader = reader;
        _unsubscribe = unsubscribe;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return; // idempotent
        }

        try
        {
            //below will call TryComplete
            _unsubscribe();
        }
        catch 
        {
            //do nothing
        }
    }
}
