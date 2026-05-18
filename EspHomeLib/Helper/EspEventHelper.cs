using System;
using System.Globalization;
using System.Text.Json;

namespace EspHomeLib.Helper;
public static class EspEventHelper
{
    public static float ConvertToFloat(this JsonElement? value)
    {
        if (value is null) return 0f;

        return value.Value.ValueKind switch
        {
            JsonValueKind.Number => value.Value.GetSingle(),
            JsonValueKind.True => 1f,
            JsonValueKind.False => 0f,
            JsonValueKind.String => float.TryParse(
                value.Value.GetString(),
                NumberStyles.Number | NumberStyles.AllowExponent,
                null, out var f) ? f : 0f,
            _ => 0f
        };
    }
}
