using System.ComponentModel.DataAnnotations;

namespace SseLib.Core.Option;

public sealed class StatusInfoOption
{
    [Required]
    public string Prefix { get; set; }
    [Required]
    public string Suffix { get; set; }
    [Required]
    public string Name { get; set; }
    [Required]
    public string Unit { get; set; }
    public float RecordDelta { get; set; } = 1;
    public int RecordThrottle { get; set; } = 60;
    public bool Hidden { get; set; } = false;
    public string? GroupInfoName { get; set; } = null;

    public override string ToString()
    {
        return $"Prefix {Prefix}, Suffix {Suffix}, Name {Name}, Unit {Unit}";
    }
}
