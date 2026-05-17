namespace EcoWittLib.Helper;

public static class HelperMethod
{
    private static readonly (string from, string to, Func<decimal, string> convert)[] dic_from_to = 
        [
            ("tempinf", "tempinc", FtoC),
            ("tempf", "tempc", FtoC),
            ("baromrelin", "baromrelhpa", InHgtohPa),
            ("baromabsin", "baromabshpa", InHgtohPa),
            ("winddir", "winddircardinal", DegreesToCardinalDetailed),
            ("windspeedmph", "windspeedkmh", MphtoKmh),
            ("windgustmph", "windgustkmh", MphtoKmh),
            ("maxdailygust", "maxdailykmh", MphtoKmh),
            ("rrain_piezo", "rrain_piezomm", IntoMm),
            ("erain_piezo", "erain_piezomm", IntoMm),
            ("hrain_piezo", "hrain_piezomm", IntoMm),
            ("drain_piezo", "drain_piezomm", IntoMm),
            ("wrain_piezo", "wrain_piezomm", IntoMm),
            ("mrain_piezo", "mrain_piezomm", IntoMm),
            ("yrain_piezo", "yrain_piezomm", IntoMm),
            ("runtime", "runtime_human", SectoHumain),
        ];

    private static readonly string[] cardinals = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N"];

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

    public static string IntoMm(this decimal inch)
    {
        var mm = inch * 25.4m;

        return mm.ToString("0.000");
    }

    public static string DegreesToCardinalDetailed(this decimal degrees)
    {
        degrees *= 10;
        
        return cardinals[(int)Math.Round((degrees % 3600) / 225)];
    }

    private static string SectoHumain(this decimal second)
    {
        var result = TimeSpan.FromSeconds((long)second).ToString(@"d'd 'h'h 'm'm 's's'");

        return result;
    }
}
