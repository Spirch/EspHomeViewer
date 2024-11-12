using EspHomeLib.Option;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Dto;
public  class ProcessOption
{
    public DeviceInfoOption DeviceInfo { get; set; }
    public StatusInfoOption StatusInfo { get; set; }
    public GroupInfoOption GroupInfo { get; set; }
}
