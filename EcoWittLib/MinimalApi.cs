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
                                               EventBroadcaster<EcoWittSse, string> broadcaster) =>
        {
            if(httpContext.Connection.LocalPort == 5163)
            {
                //logger.LogInformation("Post from {ip}", httpContext.Connection.RemoteIpAddress);

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
                    Console.WriteLine(ex);
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
                                     EventBroadcaster<EcoWittSse, string> broadcaster,
                                     CancellationToken ct) =>
        {
            logger.LogInformation("Get Stream from {ip}", httpContext.Connection.RemoteIpAddress);

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
                //broadcaster.Unsubscribe(httpContext.TraceIdentifier);
            }

            logger.LogInformation("Close Stream from {ip}", httpContext.Connection.RemoteIpAddress);
        });
    }
}
