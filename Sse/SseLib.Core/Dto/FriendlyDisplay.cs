using System;

namespace SseLib.Core.Dto;

public sealed class FriendlyDisplay
{
    public string DeviceName { get; set; }
    public string Name { get; set; }
    public float? Data { get; set; }
    public string Unit { get; set; }
    public string GroupInfo { get; set; }
    public DateTime LastUpdate { get; set; }

    public override string ToString()
    {
        return $"{DeviceName}: {Name} {Data:0.##} {Unit} {LastUpdate}";
    }
}
