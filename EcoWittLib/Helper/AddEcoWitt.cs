using ChannelLib;
using EcoWittLib.SSE;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace EcoWittLib.Helper;

public static class AddEspHomeLib
{
    public static IServiceCollection AddEcoWitt(this IServiceCollection services)
    {
        services.AddSingleton<EventBroadcaster<BroadcastMessage, string>>();

        return services;
    }

    public static async Task<WebApplication> UseEcoWittAsync(this WebApplication app)
    {
        MinimalApi.Map(app);

        var broadcast = app.Services.GetRequiredService<EventBroadcaster<BroadcastMessage, string>>();
        if (broadcast != null)
        {
            await HelperMethod.StartPingAsync(broadcast);
        }

        return app;
    }
}
