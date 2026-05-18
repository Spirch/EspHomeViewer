using System;
using System.Globalization;

namespace EspHomeLib.Helper;
public static class EspEventHelper
{
    public static float ConvertToFloat(this object value)
    {
        if (value is float valDec)
            return valDec;

        if (value is null)
            return 0f;

        var valueString = value.ToString();

        if (float.TryParse(valueString, NumberStyles.Number | NumberStyles.AllowExponent, null, out var dec))
            return dec;

        if (bool.TryParse(valueString, out bool bo))
            return Convert.ToSingle(bo);

        return 0f;
    }
}
