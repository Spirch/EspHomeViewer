using EspHomeLib.Helper;
using EspHomeViewer.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot.Statistics;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSseManager(builder.Configuration);

builder.Services.AddDatabaseManager(builder.Configuration);

builder.Services.AddGraphServices();

builder.Services.AddBlazorContextMenu();

builder.Services.AddSignalR(hubOptions =>
{
    hubOptions.EnableDetailedErrors = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
