using System.ComponentModel.DataAnnotations;

namespace SseLib.Core.Option;
public class DeviceInfoOption
{
    [Required]
    public string Name { get; set; }
    [Required]
    public string DeviceName { get; set; }
    public bool IgnoreGroup { get; set; } = false;

    public override string ToString()
    {
        return $"Name {Name}, FriendlyName {DeviceName}";
    }
}
