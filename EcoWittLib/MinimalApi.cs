using ChannelLib;
using EcoWittLib.Helper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Web;

namespace EcoWittLib;

public class MinimalApi
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/weatherforecast", async (HttpContext httpContext,
                                               ILogger<MinimalApi> logger,
                                               EventBroadcaster<string, EcoWittSse> broadcaster) =>
        {
            //todo port in config
            if(httpContext.Connection.LocalPort == 5163)
            {
                if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Post from {ip}", httpContext.Connection.RemoteIpAddress);

                try
                {
                    using StreamReader stream = new(httpContext.Request.Body);
                    var body = HttpUtility.ParseQueryString(await stream.ReadToEndAsync());
                    var dict = body.AllKeys?.ToDictionary(k => k!, k => body[k]!);
                    dict?.FixWeatherData();
                    var json = JsonSerializer.Serialize(dict);

                    broadcaster.Broadcast(EcoWittSse.Create("weather", json));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "MinimalApi weatherforecast");
                }

                return Results.Ok();
            }
            else
            {
                return Results.NotFound();
            }
        });

        app.MapGet("/stream", async (HttpContext httpContext,
                                     ILogger<MinimalApi> logger,
                                     EventBroadcaster<string, EcoWittSse> broadcaster,
                                     CancellationToken ct) =>
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Get Stream from {ip}", httpContext.Connection.RemoteIpAddress);

            httpContext.Response.SetSseHeaders();

            using var subscriber = broadcaster.Subscribe(httpContext.TraceIdentifier);

            try
            {
                await foreach (var message in subscriber.Reader.ReadAllAsync(ct))
                {
                    await httpContext.Response.WriteEventAsync(message, ct: ct);
                }
            }
            catch (OperationCanceledException)
            {
            }

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("Close Stream from {ip}", httpContext.Connection.RemoteIpAddress);
        });
    }
}
