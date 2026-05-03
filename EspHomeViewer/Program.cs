using EcoWittLib;
using EcoWittLib.Helper;
using EcoWittLib.SSE;
using EspHomeLib.Helper;
using EspHomeViewer.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata.Ecma335;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSseManager(builder.Configuration);

builder.Services.AddDatabaseManager(builder.Configuration);

builder.Services.AddGraphServices();

builder.Services.AddBlazorContextMenu();

builder.Services.AddEcoWitt();

builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.EnableDetailedErrors = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

await app.UseEcoWittAsync();

app.MapGet("/", (HttpContext httpContext) => Results.NotFound());

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
