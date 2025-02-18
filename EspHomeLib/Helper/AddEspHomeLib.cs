using EspHomeLib.Database;
using EspHomeLib.HostedServices;
using EspHomeLib.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;
using System.Net;

namespace EspHomeLib.Helper;
public static class AddEspHomeLib
{
    public static IServiceCollection AddSseManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EsphomeOptions>(configuration.GetSection("EsphomeOptions"));

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
        services.AddSingleton<ProcessEvent>();
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
