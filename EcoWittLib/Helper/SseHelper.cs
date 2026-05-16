using ChannelLib;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace EcoWittLib.Helper;

public static class SseHelper
{
    public static async Task StartPingAsync(EventBroadcaster<EcoWittSse, string> broadcast, CancellationToken ct)
    {
        if (broadcast != null)
        {
            _ = Task.Run(async () =>
            {
                using PeriodicTimer ping = new(TimeSpan.FromSeconds(30));
                while (await ping.WaitForNextTickAsync(ct))
                {
                    broadcast.Broadcast(EcoWittSse.Create("keepalive", "ping"));
                }
            }, ct);
        }
    }

    public static async Task WriteEventAsync(this HttpResponse response,
                                             EcoWittSse message,
                                             CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.Append($"id: {message.Id}\n");
        sb.Append($"event: {message.EventType}\n");
        foreach (var line in message.Data.Split('\n'))
        {
            sb.Append($"data: {line}\n");
        }
        sb.Append('\n');
        await response.WriteAsync(sb.ToString(), ct);
        await response.Body.FlushAsync(ct);
    }

    public static void SetSseHeaders(this HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");
    }
}
