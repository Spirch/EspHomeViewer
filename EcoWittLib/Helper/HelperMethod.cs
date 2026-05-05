using ChannelLib;
using EcoWittLib.SSE;

namespace EcoWittLib.Helper;

public static class HelperMethod
{
    private static readonly PeriodicTimer ping = new(TimeSpan.FromSeconds(30));

    private static readonly (string from, string to, Func<decimal, string> convert)[] dic_from_to = 
        [
            ("tempinf", "tempinc", (from) => FtoC(from)),
            ("tempf", "tempc", (from) => FtoC(from)),
            ("baromrelin", "baromrelhpa", (from) => InHgtohPa(from)),
            ("baromabsin", "baromabshpa", (from) => InHgtohPa(from)),
            ("winddir", "winddircardinal", (from) => DegreesToCardinalDetailed(from)),
            ("windspeedmph", "windspeedkmh", (from) => MphtoKmh(from)),
            ("windgustmph", "windgustkmh", (from) => MphtoKmh(from)),
            ("maxdailygust", "maxdailykmh", (from) => MphtoKmh(from)),
            ("rrain_piezo", "rrain_piezomm", (from) => IntoCm(from)),
            ("erain_piezo", "erain_piezomm", (from) => IntoCm(from)),
            ("hrain_piezo", "hrain_piezomm", (from) => IntoCm(from)),
            ("drain_piezo", "drain_piezomm", (from) => IntoCm(from)),
            ("wrain_piezo", "wrain_piezomm", (from) => IntoCm(from)),
            ("mrain_piezo", "mrain_piezomm", (from) => IntoCm(from)),
            ("yrain_piezo", "yrain_piezomm", (from) => IntoCm(from))
        ];

    private static readonly string[] caridnals = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N"];

    private static readonly string[] removeItem = ["PASSKEY", "stationtype", "model", "heap", "ws90_ver", "freq", "interval", "dateutc"];

    public static void FixWeatherData(this Dictionary<string, string> dict)
    {
        if (dict.TryGetValue("dateutc", out var dateutc))
        {
            var toUtc = DateTimeOffset.Parse($"{dateutc}Z");
            var toLocal = toUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            dict["datelocal"] = toLocal;
        }

        foreach (var val in dic_from_to)
        {
            if (dict.TryGetValue(val.from, out string? dicValString) && decimal.TryParse(dicValString, out decimal dicValDec))
            {
                dict[val.to] = val.convert(dicValDec);
                dict.Remove(val.from);
            }
        }

        foreach (var rem in removeItem)
        {
            dict.Remove(rem);
        }

        dict["dewPoint"] = string.Empty;
        dict["humidex"] = string.Empty;
        dict["windChill"] = string.Empty;

        if (dict.TryGetValue("tempc", out var tempc) && decimal.TryParse(tempc, out var dectempc))
        {
            if (dict.TryGetValue("humidity", out var humidity) && decimal.TryParse(humidity, out var dechumidity))
            {
                dict["dewPoint"] = EnvironmentCanadaWeather.CalculateDewPoint(dectempc, dechumidity);
                dict["humidex"] = EnvironmentCanadaWeather.CalculateHumidex(dectempc, dechumidity);
            }

            if (dict.TryGetValue("windspeedkmh", out var windspeedkmh) && decimal.TryParse(windspeedkmh, out var decwindspeedkmh))
            {
                dict["windChill"] = EnvironmentCanadaWeather.CalculateWindChill(dectempc, decwindspeedkmh);
            }
        }
    }

    public static string FtoC(this decimal fahrenheit)
    {
        var celcius =  5.0m / 9.0m * (fahrenheit - 32m);

        return celcius.ToString("0.0");
    }

    public static string InHgtohPa(this decimal inHg)
    {
        var hPa = inHg * 33.86389m;

        return hPa.ToString("0.000");
    }

    public static string MphtoKmh(this decimal mph)
    {
        var kmh = mph * 1.609344m;

        return kmh.ToString("0.00");
    }

    public static string IntoCm(this decimal inch)
    {
        var cm = inch * 25.4m;

        return cm.ToString("0.000");
    }

    public static string DegreesToCardinalDetailed(this decimal degrees)
    {
        degrees *= 10;
        
        return caridnals[(int)Math.Round((degrees % 3600) / 225)];
    }

    public static async Task StartPingAsync(EventBroadcaster<BroadcastMessage, string> broadcast)
    {
        if (broadcast != null)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    if (await ping.WaitForNextTickAsync())
                    {
                        broadcast.Broadcast(BroadcastMessage.Create("keepalive", "ping"));
                    }
                }
            });
        }
    }
}
