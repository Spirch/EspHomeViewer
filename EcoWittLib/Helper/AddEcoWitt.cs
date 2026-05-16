using ChannelLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EcoWittLib.Helper;

public static class AddEcoWittLib
{
    public static IServiceCollection AddEcoWitt(this IServiceCollection services)
    {
        services.AddSingleton<EventBroadcaster<EcoWittSse, string>>();
        services.AddSingleton<EventBroadcaster<Dictionary<string, string>, IChannelSubscriber<string>>>();

        return services;
    }

    public static async Task<WebApplication> UseEcoWittAsync(this WebApplication app)
    {
        MinimalApi.Map(app);

        var broadcast = app.Services.GetRequiredService<EventBroadcaster<EcoWittSse, string>>();
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        await SseHelper.StartPingAsync(broadcast, lifetime.ApplicationStopping);

        return app;
    }
}
