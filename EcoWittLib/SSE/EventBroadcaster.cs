using System.Collections.Concurrent;
using System.Threading.Channels;

namespace EcoWittLib.SSE;

public class EventBroadcaster
{
    private readonly ConcurrentDictionary<string, Channel<BroadcastMessage>> _clients = new();

    public ChannelReader<BroadcastMessage> Subscribe(string client)
    {
        var channel = Channel.CreateBounded<BroadcastMessage>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });


        _clients.TryAdd(client, channel);

        return channel.Reader;
    }

    public void Unsubscribe(string client)
    {
        _clients.TryRemove(client, out var _);
    }

    public async Task BroadcastAsync(BroadcastMessage message)
    {
        foreach (var channel in _clients.Values)
        {
            await channel.Writer.WriteAsync(message);
        }
    }
}