namespace EspHomeLib.Dto;
public class FriendlyDisplay
{
    public string DeviceName { get; set; }
    public string Name { get; set; }
    public decimal? Data { get; set; }
    public string Unit { get; set; }
    public string GroupInfo { get; set; }

    public override string ToString()
    {
        return $"{DeviceName}: {Name} {Data} {Unit}";
    }
}
