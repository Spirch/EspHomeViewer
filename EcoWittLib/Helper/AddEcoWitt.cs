using ChannelLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcoWittLib.Helper;

public static class AddEcoWittLib
{
    public static IServiceCollection AddEcoWitt(this IServiceCollection services)
    {
        services.AddSingleton<EventBroadcaster<string, EcoWittSse>>();
        services.AddSingleton<EventBroadcaster<IChannelSubscriber, Dictionary<string, string>>>();

        return services;
    }

    public static async Task<WebApplication> UseEcoWittAsync(this WebApplication app)
    {
        MinimalApi.Map(app);

        var broadcast = app.Services.GetRequiredService<EventBroadcaster<string, EcoWittSse>>();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        await SseHelper.StartPingAsync(broadcast, lifetime.ApplicationStopping);

        return app;
    }
}
