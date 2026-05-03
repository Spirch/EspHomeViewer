namespace EcoWittLib.SSE;

public class BroadcastMessage
{
    private static int _id;
    public int Id { get; private set; }

    public string Data { get; private set; } = string.Empty;
    public string EventType { get; private set; } = "Undefined";

    public static BroadcastMessage Create(string eventType, string data)
    {
        return new() { Id = Interlocked.Increment(ref _id), EventType = eventType, Data = data };
    }
}
