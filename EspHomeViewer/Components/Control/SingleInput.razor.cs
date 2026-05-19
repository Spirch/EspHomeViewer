using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using SseLib.ChannelLib;
using SseLib.ChannelLib.Dto;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Control;

public partial class SingleInput : IChannelSubscriber, IDisposable
{
    [Inject]
    private ILogger<SingleInput> Logger { get; set; }

    [Inject]
    private EspHomeData EspHomeData { get; set; }

    [Parameter, EditorRequired]
    public string DeviceName { get; set; }

    [Parameter, EditorRequired]
    public string Name { get; set; }

    [Parameter, EditorRequired]
    public string Unit { get; set; }

    [Parameter, EditorRequired]
    public EventBroadcaster<IChannelSubscriber, IChannelSubscriber> ChannelSubscriberUpdate { get; set; }

    private EventSubscriber<IChannelSubscriber> channelSubscriber;
    private CancellationTokenSource channelSubscriberCT;

    private float? Data { get; set; }
    private DateTime? LastUpdate { get; set; }

    public string ChannelNameId => $"{DeviceName}.{Name}";

    private string DataTruncated => Data?.ToString("F2").Replace(".00", string.Empty);

    protected override void OnParametersSet()
    {
        var friendlyDisplay = EspHomeData.TryGetData(DeviceName, Name);

        Data = friendlyDisplay?.Data;
        LastUpdate = friendlyDisplay?.LastUpdate;

        if(!ChannelSubscriberUpdate.IsAlreadySubscribed(this))
        {
            channelSubscriber = ChannelSubscriberUpdate.Subscribe(this);
            ListenEventSubscriber();
        }
    }

    private void ListenEventSubscriber()
    {
        channelSubscriberCT = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while(!channelSubscriberCT.Token.IsCancellationRequested)
            {
                try
                {
                    await foreach (var message in channelSubscriber.Reader.ReadAllAsync(channelSubscriberCT.Token))
                    {
                        await UpdateData();
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
        }, channelSubscriberCT.Token);
    }

    private async Task UpdateData()
    {
        var display = EspHomeData.TryGetData(DeviceName, Name);

        Data = display?.Data;
        LastUpdate = display?.LastUpdate;

        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        channelSubscriberCT?.Cancel();
        channelSubscriberCT?.Dispose();
        channelSubscriber?.Dispose();
    }
}
