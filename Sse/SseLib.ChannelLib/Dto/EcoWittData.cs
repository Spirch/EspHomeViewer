using Microsoft.Extensions.Logging;
using SseLib.Core.Dto;
using SseLib.Core.Helper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SseLib.ChannelLib.Dto;

public sealed class EcoWittData
{
    private readonly ILogger<EcoWittData> _logger;
    private readonly ConcurrentDictionary<string, string> data = new();
    private readonly EventBroadcaster<IChannelSubscriber, Dictionary<string, string>> _channelSubscriberDictionnary;
    private readonly EventBroadcaster<IChannelSubscriber, IChannelSubscriber> _channelSubscriberUpdate;

    public EcoWittData(EventBroadcaster<IChannelSubscriber, Dictionary<string, string>> channelSubscriberDictionnary,
                       EventBroadcaster<IChannelSubscriber, IChannelSubscriber> channelSubscriberUpdate,
                       ILogger<EcoWittData> logger)
    {
        _logger = logger;

        _channelSubscriberDictionnary = channelSubscriberDictionnary;
        _channelSubscriberUpdate = channelSubscriberUpdate;
    }

    public bool TryGetData(string key, out string value)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetValue {key}", nameof(EcoWittData), key);

        var ok = data.TryGetValue(key, out value);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetValue {shortForm}", nameof(EcoWittData), key);

        return ok;
    }

    public void UpdateData(Dictionary<string, string> weatherData)
    {
        foreach(var item in weatherData)
        {
            data[item.Key] = item.Value;
        }

        _channelSubscriberUpdate.BroadcastByName(nameof(EcoWittData), null);
    }
}
