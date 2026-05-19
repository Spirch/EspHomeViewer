using System.Threading;

namespace EcoWittLib;

public class EcoWittSse
{
    private static long _id;
    public long Id { get; private set; }

    public string Data { get; private set; } = string.Empty;
    public string EventType { get; private set; } = "Undefined";

    public static EcoWittSse Create(string eventType, string data)
    {
        return new() { Id = Interlocked.Increment(ref _id), EventType = eventType, Data = data };
    }
}
