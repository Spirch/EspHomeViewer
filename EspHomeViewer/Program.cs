using EspHomeViewer.Components;
using EspHomeLib;
using EspHomeLib.Database;
using EspHomeLib.Option;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection.Metadata;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var dbname = builder.Configuration.GetValue<string>("DefaultConnection");
await EfContext.CreateDBIfNotExistAsync(dbname);

builder.Services.AddDbContext<EfContext>(options => options.UseSqlite($"Data Source={dbname}"), ServiceLifetime.Singleton);

builder.Services.Configure<EsphomeOptions>(builder.Configuration.GetSection("EsphomeOptions"));

builder.Services.AddHttpClient();
builder.Services.AddTransient<SseClient>();
builder.Services.AddSingleton<ProcessEvent>();

builder.Services.AddHostedService<SseClientManager>();
builder.Services.AddHostedService<DatabaseManager>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
