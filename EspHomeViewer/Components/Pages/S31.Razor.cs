using BlazorContextMenu;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SseLib.ChannelLib;
using SseLib.ChannelLib.Dto;
using SseLib.Tool;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EspHomeViewer.Components.Pages;

public partial class S31 : IDisposable
{
    [Inject]
    private EspHomeData EspHomeData { get; set; }

    [Inject]
    private GraphServices GraphServices { get; set; }

    [Inject]
    private EventBroadcaster<IChannelSubscriber, IChannelSubscriber> ChannelSubscriberUpdate { get; set; }

    [Inject] 
    IJSRuntime JS { get; set; }

    public void Dispose()
    {
        EspHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;
    }

    protected override void OnParametersSet()
    {
        EspHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;
        EspHomeData.OnEspHomeOptionChanged += OnEspHomeOptionChanged;
    }

    private void OnEspHomeOptionChanged(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
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

                if (graph != null)
                {
                    var fileName = string.Join("-", $"For-{day}-day-{data}-at-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png".Split(Path.GetInvalidFileNameChars()));
                    await DownloadFileFromStream(graph, fileName);
                }
            }
        }
    }

    private async Task DownloadFileFromStream(byte[] data, string fileName)
    {
        if (data != null)
        {
            using var fileStream = new MemoryStream(data);
            using var streamRef = new DotNetStreamReference(stream: fileStream);

            await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }
    }
}
