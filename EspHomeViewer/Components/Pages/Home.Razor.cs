using EspHomeLib;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;
using BlazorContextMenu;
using Microsoft.JSInterop;

namespace EspHomeViewer.Components.Pages;

public partial class Home : IProcessEventSubscriber, IDisposable
{
    [Inject]
    private IOptionsMonitor<EsphomeOptions> EsphomeOptions { get; set; }

    [Inject]
    private ProcessEvent ProcessEvent { get; set; }

    [Inject]
    private ILogger<Home> Logger { get; set; }

    [Inject]
    private GraphServices GraphServices { get; set; }


    [Inject] 
    IJSRuntime JS { get; set; }

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
        await Task.CompletedTask;

        return false;

        //if(!alreadyCollected && ++eventCount > 500)
        //{
        //    if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("{Class} GcCollected Start", nameof(Home));

        //    GC.Collect();
        //    eventCount = 0;

        //    alreadyCollected = true;

        //    if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("{Class} GcCollected Stop", nameof(Home));
        //}
        //else if (alreadyCollected)
        //{
        //    eventCount = 0;
        //}

        //await Task.CompletedTask;

        //return alreadyCollected;
    }

    private async Task OnMenuGraphClickAsync(ItemClickEventArgs e)
    {
        string name;

        if(int.TryParse(e.MenuItem.Id, out int day))
        {
            if (e.MenuItem.ParentComponent is SubMenu subMenu)
            {
                name = string.Format(subMenu.Id, e.ContextMenuTargetId);
            }
            else
            {
                name = e.ContextMenuTargetId;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var graph = await GraphServices.GraphAsync(name, day);

                await DownloadFileFromStream(graph);
            }
        }
    }

    private async Task DownloadFileFromStream(byte[] data)
    {
        using var fileStream = new MemoryStream(data);
        using var streamRef = new DotNetStreamReference(stream: fileStream);

        await JS.InvokeVoidAsync("downloadFileFromStream", "img.png", streamRef);
    }
}
