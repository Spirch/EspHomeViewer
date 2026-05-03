using EspHomeLib;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Pages;

public partial class EcoWitt : IProcessEventSubscriber, IDataCanReceive, IDisposable
{
    [Inject]
    private ProcessEvent ProcessEvent { get; set; }

    [Inject]
    IJSRuntime JS { get; set; }

    private Dictionary<string, string> weatherData = new();
    private Subscriber subscriber;

    protected override void OnParametersSet()
    {
        subscriber = ProcessEvent.Subscribe(this);

        subscriber.DataReceives.TryAdd(Random.Shared.Next().ToString(), this);
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

    public async Task ReceiveDataAsync(Dictionary<string, string> data)
    {
        foreach (var d in data)
        {
            weatherData[d.Key] = d.Value;
        }

        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ProcessEvent.Unsubscribe(this);
    }
}
