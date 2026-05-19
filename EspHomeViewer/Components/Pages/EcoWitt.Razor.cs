using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SseLib.ChannelLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Pages;

public partial class EcoWitt : IChannelSubscriber, IDisposable
{
    [Inject]
    private EventBroadcaster<IChannelSubscriber, Dictionary<string, string>> ChannelSubscriber { get; set; }

    [Inject]
    IJSRuntime JS { get; set; }

    public string ChannelNameId => nameof(EcoWitt);

    private readonly ConcurrentDictionary<string, string> weatherData = new();

    private EventSubscriber<Dictionary<string, string>> eventSubscriber;
    private CancellationTokenSource weatherDataCT;

    protected override void OnParametersSet()
    {
        if (!ChannelSubscriber.IsAlreadySubscribed(this))
        {
            eventSubscriber = ChannelSubscriber.Subscribe(this);
            ListenEventSubscriber();
        }
    }

    private void ListenEventSubscriber()
    {
        weatherDataCT = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!weatherDataCT.Token.IsCancellationRequested)
            {
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
                catch (Exception ex)
                {
                    //do nothing
                }
            }
        }, weatherDataCT.Token);
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
