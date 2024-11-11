using System;
using System.Globalization;

namespace EspHomeLib.Dto;

public class EspEvent
{
    public override string ToString()
    {
        return $"Id: {Id}, Value: {Value}, Name: {Name}, State: {State}, Event_Type: {Event_Type}, Data: {Data}, UnixTimeMs: {UnixTime}";
    }

    public string Id { get; set; }
    public object Value { get; set; }
    public string Name { get; set; }
    public string State { get; set; }
    public string Event_Type { get; set; }

    public long UnixTime { get; set; }
    public decimal Data
    {
        get
        {
            return ConvertValue();
        }
        set
        {
            Value = value;
        }
    }

    private decimal ConvertValue()
    {
        if (Value is decimal valDec)
            return valDec;

        if (Value is null)
            return 0m;

        if (decimal.TryParse(Value.ToString(), NumberStyles.Number | NumberStyles.AllowExponent, null, out decimal dec))
            return Truncate(dec, 2);

        if (bool.TryParse(Value.ToString(), out bool bo))
            return Convert.ToDecimal(bo);

        return 0m;
    }

    private static decimal Truncate(decimal d, byte decimals)
    {
        decimal r = Math.Round(d, decimals);

        if (d > 0 && r > d)
        {
            return r - new decimal(1, 0, 0, false, decimals);
        }
        else if (d < 0 && r < d)
        {
            return r + new decimal(1, 0, 0, false, decimals);
        }

        return r;
    }
}
