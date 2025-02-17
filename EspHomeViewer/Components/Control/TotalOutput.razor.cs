﻿using EspHomeLib;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Control;

public partial class TotalOutput : IEventCanReceive
{
    [Inject]
    private ProcessEvent ProcessEvent { get; set; }

    [Parameter, EditorRequired]
    public string GroupInfo { get; set; }

    [Parameter, EditorRequired]
    public string Unit { get; set; }

    [Parameter, EditorRequired]
    public Subscriber Subscriber { get; set; }

    private decimal? Data { get; set; }
    private DateTime? LastUpdate { get; set; }

    protected override void OnParametersSet()
    {
        Data = ProcessEvent.TryGetSumValue(GroupInfo);
        LastUpdate = DateTime.Now;

        Subscriber.EventGroupCanReceives.TryAdd(GroupInfo, this);
    }

    public async Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay)
    {
        Data = ProcessEvent.TryGetSumValue(GroupInfo);
        LastUpdate = DateTime.Now;

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
