using ChannelLib;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Pages;

public partial class EcoWitt : IChannelSubscriber, IDisposable
{
    [Inject]
    private EventBroadcaster<Dictionary<string, string>, IChannelSubscriber> ChannelSubscriber { get; set; }

    [Inject]
    IJSRuntime JS { get; set; }

    private readonly Dictionary<string, string> weatherData = new();

    private EventSubscriber<Dictionary<string, string>> eventSubscriber;
    private CancellationTokenSource weatherDataCT;

    protected override void OnParametersSet()
    {
        eventSubscriber = ChannelSubscriber.Subscribe(this);
        ListenEventSubscriber();
    }

    private void ListenEventSubscriber()
    {
        _ = Task.Run(async () =>
        {
            weatherDataCT = new CancellationTokenSource();
            try
            {
                await foreach (var message in eventSubscriber.Reader.ReadAllAsync(weatherDataCT.Token))
                {
                    foreach (var d in message)
                    {
                        weatherData[d.Key] = d.Value;
                    }

                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("observeAllTables");
        }
    }

    private string TryReadWeatherData(string key)
    {
        if (weatherData.TryGetValue(key, out var value))
        {
            return value;
        }

        return string.Empty;
    }

    public void Dispose()
    {
        weatherDataCT?.Cancel();
        eventSubscriber?.Dispose();
    }
}
