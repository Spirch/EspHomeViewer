using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Helper;
public static class EspEventHelper
{
    public static decimal ConvertToDecimal(this object value)
    {
        if (value is decimal valDec)
            return valDec;

        if (value is null)
            return 0m;

        var valueString = value.ToString();

        if (decimal.TryParse(valueString, NumberStyles.Number | NumberStyles.AllowExponent, null, out decimal dec))
            return dec.TruncateDecimal(2);

        if (bool.TryParse(valueString, out bool bo))
            return Convert.ToDecimal(bo);

        return 0m;
    }

    private static decimal TruncateDecimal(this decimal d, byte decimals)
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
