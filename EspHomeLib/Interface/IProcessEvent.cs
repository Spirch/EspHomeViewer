using EspHomeLib.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Interface;
public interface IProcessEvent
{
    Task SendAsync(EspEvent espEvent, Uri uri);
    Task SendAsync(Exception exception, Uri uri);
    Task SendAsync(string data, Uri uri);
}
