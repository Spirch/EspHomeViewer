namespace EspHomeLib.Option;
public class DeviceInfoOption
{
    public string Name { get; set; }
    public string DeviceName { get; set; }
    public bool IgnoreGroup { get; set; }

    public override string ToString()
    {
        return $"Name {Name}, FriendlyName {DeviceName}";
    }
}
