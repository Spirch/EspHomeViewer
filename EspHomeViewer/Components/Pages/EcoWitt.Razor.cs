using EspHomeViewer.Components.Control;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SseLib.ChannelLib;
using SseLib.ChannelLib.Dto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Pages;

public partial class EcoWitt : IChannelSubscriber, IDisposable
{
    [Inject]
    private ILogger<EcoWitt> Logger { get; set; }

    [Inject]
    private EcoWittData EcoWittData { get; set; }

    [Inject]
    private EventBroadcaster<IChannelSubscriber, IChannelSubscriber> ChannelSubscriber { get; set; }

    [Inject]
    IJSRuntime JS { get; set; }

    public string ChannelNameId => nameof(EcoWittData);

    private EventSubscriber<IChannelSubscriber> eventSubscriber;
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
                        await InvokeAsync(StateHasChanged);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "ListenEventSubscriber");
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
        if (EcoWittData.TryGetData(key, out var value))
        {
            return value;
        }

        return string.Empty;
    }

    public void Dispose()
    {
        weatherDataCT?.Cancel();
        weatherDataCT?.Dispose();
        eventSubscriber?.Dispose();
    }
}
