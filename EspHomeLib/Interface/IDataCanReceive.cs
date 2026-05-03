using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Interface;

public interface IDataCanReceive
{
    public Task ReceiveDataAsync(Dictionary<string, string> data);
}
