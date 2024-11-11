using EspHomeLib;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Pages;

public partial class Home : IProcessEventSubscriber, IDisposable
{
    [Inject]
    private IOptionsMonitor<EsphomeOptions> EsphomeOptions { get; set; }

    [Inject]
    private ProcessEvent ProcessEvent { get; set; }

    [Inject]
    private ILogger<Home> Logger { get; set; }

    private Subscriber subscriber;
    private int eventCount;

    public void Dispose()
    {
        ProcessEvent.Unsubscribe(this);
    }

    protected override void OnParametersSet()
    {
        subscriber = ProcessEvent.Subscribe(this);
    }

    public async Task<bool> GcCollected(bool alreadyCollected)
    {
        if(!alreadyCollected && ++eventCount > 500)
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("{Class} GcCollected Start", nameof(Home));

            //GC.Collect();
            eventCount = 0;

            alreadyCollected = true;

            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("{Class} GcCollected Stop", nameof(Home));
        }
        else if (alreadyCollected)
        {
            eventCount = 0;
        }

        await Task.CompletedTask;

        return alreadyCollected;
    }
}
