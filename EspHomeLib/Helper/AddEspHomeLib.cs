using EspHomeLib.Database;
using EspHomeLib.HostedServices;
using EspHomeLib.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EspHomeLib.Helper;
public static class AddEspHomeLib
{
    public static IServiceCollection AddSseManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EsphomeOptions>(configuration.GetSection("EsphomeOptions"));
        services.AddHttpClient();
        services.AddTransient<SseClient>();
        services.AddSingleton<ProcessEvent>();
        services.AddHostedService<SseClientManager>();

        return services;
    }

    public static IServiceCollection AddDatabaseManager(this IServiceCollection services, IConfiguration configuration)
    {
        var dbname = configuration.GetValue<string>("DefaultConnection");
        EfContext.CreateDBIfNotExist(dbname);
        services.AddHostedService<DatabaseManager>();
        services.AddDbContext<EfContext>(options => options.UseSqlite($"Data Source={dbname}"), ServiceLifetime.Singleton);

        return services;
    }
}
