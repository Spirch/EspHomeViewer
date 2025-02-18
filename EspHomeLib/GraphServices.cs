using EspHomeLib.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot;
using ScottPlot.AxisPanels;
using ScottPlot.TickGenerators;
using ScottPlot.TickGenerators.TimeUnits;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EspHomeLib;

public static class Key
{
    public const int Graph1DayValue = 1;
    public const int Graph3DaysValue = 3;
    public const int Graph7DaysValue = 7;
    public const int Graph14DaysValue = 14;
    public const int Graph30DaysValue = 30;
    public const int GraphAllValue = 0;
}

public class GraphServices
{
    private readonly EfContext _efContext;

    public GraphServices(EfContext efContext)
    {
        _efContext = efContext;
    }

    public async Task<byte[]> GraphAsync(string name, string friendlyName, int days)
    {
        byte[] result = null;

        var meta = await _efContext.RowEntry
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(x => x.Name == name &&
                                   (string.IsNullOrEmpty(friendlyName) || x.FriendlyName == friendlyName));

        if (meta != null)
        {
            var unixFilter = days > 0 ? DateTimeOffset.Now.AddDays(-days).ToUnixTimeSeconds() : 0;
            var data = await _efContext.Event.Where(x => x.UnixTime >= unixFilter && x.RowEntryId == meta.RowEntryId).AsNoTracking().ToListAsync();

            var xs = data.Select(x => DateTimeOffset.FromUnixTimeSeconds(x.UnixTime).LocalDateTime).ToList();
            var ys = data.Select(x => x.Data).ToList();

            using var myPlot = new Plot();

            var plotData = myPlot.Add.ScatterPoints(xs, ys);

            var interval = myPlot.Axes.DateTimeTicksBottom();
            SetTickGenerator(days, interval);

            plotData.LegendText = $"{meta.FriendlyName} - {meta.Unit}";
            myPlot.ShowLegend();
            myPlot.Axes.AutoScaler = new ScottPlot.AutoScalers.FractionalAutoScaler(.01, .015);
            myPlot.Axes.AutoScale();

            result = myPlot.GetImageBytes(10000, 1000, ImageFormat.Png);

            xs.Clear(); xs.TrimExcess();
            ys.Clear(); ys.TrimExcess();
        }

        return result;
    }

    private static void SetTickGenerator(int days, DateTimeXAxis interval)
    {
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
    }
}
