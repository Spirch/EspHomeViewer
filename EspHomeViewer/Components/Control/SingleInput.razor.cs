using EspHomeLib;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Control;

public partial class SingleInput : IEventCanReceive
{
    [Inject]
    private ProcessEvent ProcessEvent { get; set; }

    [Parameter, EditorRequired]
    public string DeviceName { get; set; }

    [Parameter, EditorRequired]
    public string Name { get; set; }

    [Parameter, EditorRequired]
    public string Unit { get; set; }

    [Parameter, EditorRequired]
    public Subscriber Subscriber { get; set; }

    private decimal? Data { get; set; }
    private DateTime? LastUpdate { get; set; }

    protected override void OnParametersSet()
    {
        var friendlyDisplay = ProcessEvent.TryGetData(DeviceName, Name);

        Data = friendlyDisplay?.Data;
        LastUpdate = friendlyDisplay?.LastUpdate;

        Subscriber.EventSingleCanReceives.TryAdd((DeviceName, Name), this);
    }

    public async Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay)
    {
        Data = friendlyDisplay?.Data;
        LastUpdate = friendlyDisplay?.LastUpdate;

        await InvokeAsync(StateHasChanged);
    }

    public async Task ReceiveDataAsync(Exception exception, Uri uri)
    {
        await Task.CompletedTask;
    }

    public async Task ReceiveDataAsync(string rawMessage)
    {
        await Task.CompletedTask;
    }

    public async Task ReceiveRawDataAsync(EspEvent espEvent)
    {
        await Task.CompletedTask;
    }
}
