using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Option;
public class GroupInfoOption
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Unit { get; set; }
    public int RecordThrottle { get; set; }

    public override string ToString()
    {
        return $"Name {Name}, Title {Title}, Unit {Unit}";
    }
}
