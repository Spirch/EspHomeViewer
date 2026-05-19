using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SseLib.ChannelLib;
using SseLib.Client;
using SseLib.Core.Dto;
using SseLib.Core.Option;
using SseLib.Database;
using SseLib.Database.Context;
using SseLib.Tool;
using System;
using System.Net;
using System.Net.Sockets;

namespace SseLib;
public static class AddEspHomeLib
{
    public static IServiceCollection AddSseManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EsphomeOptions>(configuration.GetSection("EsphomeOptions"));

        services.AddSingleton<EventBroadcaster<IChannelSubscriber, Exception>>();
        services.AddSingleton<EventBroadcaster<IChannelSubscriber, EspEvent>>();
        services.AddSingleton<EventBroadcaster<IChannelSubscriber, IChannelSubscriber>>();
        services.AddSingleton<EspHomeData>();

        services.AddHttpClient("sseClient").UseSocketsHttpHandler((handler, _) =>
            handler.ConnectCallback = async (ctx, ct) =>
            {
                DnsEndPoint dnsEndPoint = ctx.DnsEndPoint;
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(dnsEndPoint.Host, dnsEndPoint.AddressFamily, ct);
                var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);

                    await s.ConnectAsync(addresses, dnsEndPoint.Port, ct);
                    return new NetworkStream(s, ownsSocket: true);
                }
                catch
                {
                    s.Dispose();
                    throw;
                }
            });

        services.AddTransient<SseClient>();
        services.AddHostedService<SseClientManager>();

        return services;
    }

    public static IServiceCollection AddDatabaseManager(this IServiceCollection services, IConfiguration configuration)
    {
        var dbname = configuration.GetValue<string>("DefaultConnection");
        EfContext.CreateDBIfNotExist(dbname);
        services.AddDbContext<EfContext>(options => options.UseSqlite($"Data Source={dbname}"));
        services.AddHostedService<DatabaseManager>();

        return services;
    }

    public static IServiceCollection AddGraphServices(this IServiceCollection services)
    {
        services.AddTransient<GraphServices>();

        return services;
    }
}
