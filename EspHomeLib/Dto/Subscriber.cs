using EspHomeLib.Interface;
using System;
using System.Collections.Concurrent;

namespace EspHomeLib.Dto;
public class Subscriber : IDisposable
{
    public ConcurrentDictionary<(string, string), IEventCanReceive> EventSingleCanReceives { get; set; } = new();
    public ConcurrentDictionary<string, IEventCanReceive> EventGroupCanReceives { get; set; } = new();
    public IEventCanReceive OnEvent { get; set; }

    private IProcessEventSubscriber _instance;

    public Subscriber(IProcessEventSubscriber instance)
    {
        _instance = instance;
    }

    public void Dispose()
    {
        EventSingleCanReceives.Clear();
        EventGroupCanReceives.Clear();
        OnEvent = null;
        _instance = null;
    }
}
