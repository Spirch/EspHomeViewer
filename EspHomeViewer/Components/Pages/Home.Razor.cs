﻿using EspHomeLib;
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

    public void Dispose()
    {
        ProcessEvent.Unsubscribe(this);
    }

    protected override void OnParametersSet()
    {
        subscriber = ProcessEvent.Subscribe(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("observeAllTables");
        }
    }

    private async Task OnMenuGraphClickAsync(ItemClickEventArgs e)
    {
        string name;
        string data = null;

        if (e.Data is string)
        {
            data = e.Data.ToString();
        }

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
                var graph = await GraphServices.GraphAsync(name, data, day);
                var fileName = string.Join("-", $"For-{day}-day-{data}-at-{DateTime.Now}.png".Split(Path.GetInvalidFileNameChars()));

                await DownloadFileFromStream(graph, fileName);
            }
        }
    }

    private async Task DownloadFileFromStream(byte[] data, string fileName)
    {
        using var fileStream = new MemoryStream(data);
        using var streamRef = new DotNetStreamReference(stream: fileStream);

        await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }
}
