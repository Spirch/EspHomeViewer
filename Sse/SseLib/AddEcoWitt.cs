using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SseLib.Api;
using SseLib.Api.Helper;
using SseLib.ChannelLib;
using SseLib.Core.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SseLib;

public static class AddEcoWittLib
{
    public static IServiceCollection AddEcoWitt(this IServiceCollection services)
    {
        services.AddSingleton<EventBroadcaster<string, EcoWittSse>>();
        services.AddSingleton<EventBroadcaster<IChannelSubscriber, Dictionary<string, string>>>();

        return services;
    }

    public static WebApplication UseEcoWitt(this WebApplication app)
    {
        MinimalApi.Map(app);

        var broadcast = app.Services.GetRequiredService<EventBroadcaster<string, EcoWittSse>>();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        SseHelper.StartPing(broadcast, lifetime.ApplicationStopping);

        return app;
    }
}
