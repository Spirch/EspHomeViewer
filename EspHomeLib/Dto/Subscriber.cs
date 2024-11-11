using EspHomeLib.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EspHomeLib.Dto;
public class Subscriber : IDisposable
{
    public ConcurrentDictionary<(string, string), IEventCanReceive> EventSingleCanReceives { get; set; } = new();
    public ConcurrentDictionary<string, IEventCanReceive> EventGroupCanReceives { get; set; } = new();
    public IEventCanReceive EveryRawEvent { get; set; }

    public void Dispose()
    {
        EventSingleCanReceives.Clear(); 
        EventGroupCanReceives.Clear();
        EveryRawEvent = null;
    }
}
