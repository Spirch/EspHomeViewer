using EspHomeLib.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Interface;
public interface IEventCanReceive
{
    public Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay);
    public Task ReceiveDataAsync(Exception exception);
    public Task ReceiveDataAsync(string rawMessage);
    public Task ReceiveRawDataAsync(EspEvent espEvent);
}
