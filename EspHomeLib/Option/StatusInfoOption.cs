namespace EspHomeLib.Option;
public class StatusInfoOption
{
    public string Prefix { get; set; }
    public string Suffix { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal RecordDelta { get; set; }
    public int RecordThrottle { get; set; }
    public bool Hidden { get; set; }

    public GroupInfoOption GroupInfo { get; set; }

    public override string ToString()
    {
        return $"Prefix {Prefix}, Suffix {Suffix}, Name {Name}, Unit {Unit}";
    }
}
