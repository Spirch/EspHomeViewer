using ChannelLib;
using Microsoft.AspNetCore.Components;
using SseLib.Core.Dto;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Control;

public partial class TotalOutput : IChannelSubscriber,  IDisposable
{
    [Inject]
    private EspHomeData EspHomeData { get; set; }

    [Parameter, EditorRequired]
    public string GroupInfo { get; set; }

    [Parameter, EditorRequired]
    public string Unit { get; set; }

    [Parameter, EditorRequired]
    public EventBroadcaster<IChannelSubscriber, IChannelSubscriber> ChannelSubscriberUpdate { get; set; }

    private EventSubscriber<IChannelSubscriber> channelSubscriber;
    private CancellationTokenSource channelSubscriberCT;

    private float? Data { get; set; }
    private DateTime? LastUpdate { get; set; }

    public string ChannelNameId => GroupInfo; 
    private string DataTruncated => Data?.ToString("F2").Replace(".00", string.Empty);

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
            while (!channelSubscriberCT.Token.IsCancellationRequested)
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
                    //double nothing
                }
            }
        }, channelSubscriberCT.Token);
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
