using ChannelLib;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Control;

public partial class TotalOutput : IChannelSubscriber<string>, IEspHomeUpdate, IDisposable
{
    [Inject]
    private EspHomeData EspHomeData { get; set; }

    [Parameter, EditorRequired]
    public string GroupInfo { get; set; }

    [Parameter, EditorRequired]
    public string Unit { get; set; }

    [Parameter, EditorRequired]
    public EventBroadcaster<IChannelSubscriber<string>, IEspHomeUpdate> ChannelSubscriberUpdate { get; set; }

    private EventSubscriber<IEspHomeUpdate> channelSubscriber;
    private CancellationTokenSource channelSubscriberCT;

    private decimal? Data { get; set; }
    private DateTime? LastUpdate { get; set; }

    public string ChannelNameId => GroupInfo;

    protected override void OnParametersSet()
    {
        Data = EspHomeData.TryGetSumValue(GroupInfo);
        LastUpdate = DateTime.Now;

        if (!ChannelSubscriberUpdate.IsAlreadySubscribed(this))
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
        });
    }

    private async Task UpdateData()
    {
        Data = EspHomeData.TryGetSumValue(GroupInfo);
        LastUpdate = DateTime.Now;

        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        channelSubscriberCT?.Cancel();
        channelSubscriber?.Dispose();
    }
}
