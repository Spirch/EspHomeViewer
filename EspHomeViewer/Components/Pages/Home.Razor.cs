using EspHomeLib;
using EspHomeLib.Database;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ScottPlot;
using ScottPlot.TickGenerators;
using ScottPlot.TickGenerators.TimeUnits;
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
    private IServiceProvider ServiceProvider { get; set; }


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

    public static class Key
    {
        public const int Graph1DayValue = 1;
        public const int Graph3DaysValue = 3;
        public const int Graph7DaysValue = 7;
        public const int Graph14DaysValue = 14;
        public const int Graph30DaysValue = 30;
        public const int GraphAllValue = 0;
    }

    public async Task GraphAsync(string name, int days)
    {
        var unixFilter = days > 0 ? DateTimeOffset.Now.AddDays(-days).ToUnixTimeSeconds() : 0;
        var length = days > 0 ? $"{days}days" : "all";
        var saveFolder = Directory.CreateDirectory($"Graph-{length}-" + DateTime.Now.ToString("yyyyMMddHHmmss"));

        var EspHomeDb = ServiceProvider.GetRequiredService<EfContext>();

        var meta = await EspHomeDb.RowEntry.AsNoTracking().FirstOrDefaultAsync(x => x.Name == name);

        if (meta != null)
        {
            var data = await EspHomeDb.Event.Where(x => x.UnixTime >= unixFilter && x.RowEntryId == meta.RowEntryId).AsNoTracking().ToListAsync();

            var xs = data.Select(x => DateTimeOffset.FromUnixTimeSeconds(x.UnixTime).LocalDateTime).ToList();
            var ys = data.Select(x => x.Data).ToList();

            using var myPlot = new Plot();

            var plotData = myPlot.Add.ScatterPoints(xs, ys);

            var interval = myPlot.Axes.DateTimeTicksBottom();
            switch (days)
            {
                case Key.Graph1DayValue:
                    interval.TickGenerator = new DateTimeFixedInterval(new Minute(), 15);
                    break;
                case Key.Graph3DaysValue:
                    interval.TickGenerator = new DateTimeFixedInterval(new Minute(), 30);
                    break;
                case Key.Graph7DaysValue:
                    interval.TickGenerator = new DateTimeFixedInterval(new Minute(), 90);
                    break;
                case Key.Graph14DaysValue:
                    interval.TickGenerator = new DateTimeFixedInterval(new Hour(), 3);
                    break;
                case Key.Graph30DaysValue:
                    interval.TickGenerator = new DateTimeFixedInterval(new Hour(), 6);
                    break;
                case Key.GraphAllValue:
                    interval.TickGenerator = new DateTimeFixedInterval(new Hour(), 12);
                    break;
                default:
                    break;
            }

            plotData.LegendText = $"{meta.FriendlyName} - {meta.Unit}";
            myPlot.ShowLegend();
            myPlot.Axes.AutoScaler = new ScottPlot.AutoScalers.FractionalAutoScaler(.01, .015);
            myPlot.Axes.AutoScale();

            await DownloadFileFromStream(myPlot.GetImageBytes(10000, 1000, ImageFormat.Png));

            xs.Clear(); xs.TrimExcess();
            ys.Clear(); ys.TrimExcess();
        }
    }

    private async Task OnClick(ItemClickEventArgs e)
    {
        string name = null;
        int day = int.Parse(e.MenuItem.Id);

        if(e.MenuItem.ParentComponent is SubMenu subMenu)
        {
            name = string.Format(subMenu.Id, e.ContextMenuTargetId);
        }
        else
        {
            name = e.ContextMenuTargetId;
        }

        Console.WriteLine($"name: {name}, day: {day}");

        await GraphAsync(name, day);
    }

    private async Task DownloadFileFromStream(byte[] data)
    {
        using var fileStream = new MemoryStream(data);
        var fileName = "img.png";

        using var streamRef = new DotNetStreamReference(stream: fileStream);

        await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }
}
