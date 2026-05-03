using Microsoft.AspNetCore.Http;

namespace EcoWittLib.SSE;

public static class SseWriter
{
    public static async Task WriteEventAsync(
        HttpResponse response,
        BroadcastMessage message,
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

    public static void SetSseHeaders(HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");
    }
}