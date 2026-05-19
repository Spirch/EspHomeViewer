using System;

namespace SseLib.Database.Helper;

public static class GenericHelper
{
    public static long DiffUnixTime(this DateTimeOffset utc, long comparedTo)
    {
        return utc.ToUnixTimeSeconds() - comparedTo;
    }
}
