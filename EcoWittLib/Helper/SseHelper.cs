using ChannelLib;
using Microsoft.AspNetCore.Http;

namespace EcoWittLib.Helper;

public static class SseHelper
{
    private static readonly PeriodicTimer ping = new(TimeSpan.FromSeconds(30));
    public static async Task StartPingAsync(EventBroadcaster<EcoWittSse, string> broadcast)
    {
        if (broadcast != null)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    if (await ping.WaitForNextTickAsync())
                    {
                        broadcast.Broadcast(EcoWittSse.Create("keepalive", "ping"));
                    }
                }
            });
        }
    }

    public static async Task WriteEventAsync(this HttpResponse response,
                                             EcoWittSse message,
                                             CancellationToken ct = default)
    {
        await response.WriteAsync($"id: {message.Id}\n", ct);
        await response.WriteAsync($"event: {message.EventType}\n", ct);

        // Handle multi-line data
        foreach (var line in message.Data.Split('\n'))
        {
            await response.WriteAsync($"data: {line}\n", ct);
        }

        await response.WriteAsync("\n", ct);
        await response.Body.FlushAsync(ct);
    }

    public static void SetSseHeaders(this HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");
    }
}
