using EspHomeLib.Dto;
using System;
using System.Threading.Tasks;

namespace EspHomeLib.Interface;
public interface IEventCanReceive
{
    public Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay);
    public Task ReceiveDataAsync(Exception exception, Uri uri);
    public Task ReceiveDataAsync(string rawMessage);
    public Task ReceiveRawDataAsync(EspEvent espEvent);
}
