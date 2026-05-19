using System.ComponentModel.DataAnnotations;

namespace SseLib.Core.Option;
public class GroupInfoOption
{
    [Required]
    public string Id { get; set; }
    [Required]
    public string Name { get; set; }
    [Required]
    public string Title { get; set; }
    [Required]
    public string Unit { get; set; }
    [Required]
    public int RecordThrottle { get; set; }

    public override string ToString()
    {
        return $"Name {Name}, Title {Title}, Unit {Unit}";
    }
}
