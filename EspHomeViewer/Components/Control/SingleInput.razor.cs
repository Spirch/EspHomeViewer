using EspHomeLib.Dto;
using EspHomeLib;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;
using EspHomeLib.Interface;

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

    protected override void OnParametersSet()
    {
        Data = ProcessEvent.TryGetValue(DeviceName, Name)?.Data;
        Subscriber.EventSingleCanReceives.TryAdd((DeviceName, Name), this);
    }

    public async Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay)
    {
        Data = friendlyDisplay.Data;

        await InvokeAsync(StateHasChanged);
    }

    public async Task ReceiveDataAsync(Exception exception)
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
