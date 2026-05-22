using System.ComponentModel.DataAnnotations;

namespace SseLib.Core.Option;

public sealed class SseClientOption
{
    [Range(1,30)]
    public int PingTimeout { get; set; } = 1;
    [Range(1, 30)]
    public int PingDelay { get; set; } = 5;
    [Range(60, 300)]
    public int TimeoutDelay { get; set; } = 120;
    [Range(100, 5000)]
    public int ChannelLimit { get; set; } = 100;
    public int EcoWittServerPort { get; set; } = 5163;

    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    public override string ToString()
    {
        return $"PingDelay {PingDelay}, TimeoutDelay {TimeoutDelay}";
    }
}
