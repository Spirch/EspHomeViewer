using ChannelLib;
using EcoWittLib.Helper;
using EcoWittLib.SSE;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Web;

namespace EcoWittLib;

public class MinimalApi
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/weatherforecast", async (HttpContext httpContext,
                                               ILogger<MinimalApi> logger,
                                               EventBroadcaster<BroadcastMessage, string> broadcaster) =>
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

                    broadcaster.Broadcast(BroadcastMessage.Create("weather", json));
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
                                     EventBroadcaster<BroadcastMessage, string> broadcaster,
                                     CancellationToken ct) =>
        {
            logger.LogInformation("Get Stream from {ip}", httpContext.Connection.RemoteIpAddress);


            SseWriter.SetSseHeaders(httpContext.Response);

            using var subscriber = broadcaster.Subscribe(httpContext.TraceIdentifier);

            try
            {
                await foreach (var message in subscriber.Reader.ReadAllAsync(ct))
                {
                    await SseWriter.WriteEventAsync(httpContext.Response, message, ct: ct);
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
