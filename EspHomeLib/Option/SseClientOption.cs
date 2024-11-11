namespace EspHomeLib.Option;
public class SseClientOption
{
    public int PingTimeout { get; set; } = 1;
    public int PingDelay { get; set; } = 5;
    public int TimeoutDelay { get; set; } = 120;

    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    public override string ToString()
    {
        return $"PingDelay {PingDelay}, TimeoutDelay {TimeoutDelay}";
    }
}
