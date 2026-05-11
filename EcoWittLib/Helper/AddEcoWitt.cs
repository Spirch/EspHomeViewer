using ChannelLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace EcoWittLib.Helper;

public static class AddEspHomeLib
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
        if (broadcast != null)
        {
            await SseHelper.StartPingAsync(broadcast);
        }

        return app;
    }
}
