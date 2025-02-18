using EspHomeLib.Dto;
using EspHomeLib.Helper;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq;
using System.Threading.Tasks;

namespace EspHomeLib;
public class ProcessEvent : IProcessEvent, IDisposable
{
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly ILogger<ProcessEvent> _logger;

    private readonly ConcurrentDictionary<(string, string), FriendlyDisplay> dataDisplay = new();

    private readonly ConcurrentDictionary<IProcessEventSubscriber, Subscriber> subscriber = new();

    public FrozenDictionary<string, ProcessOption> DeviceInfo { get; set; }

    public ProcessEvent(IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor, ILogger<ProcessEvent> logger)
    {
        _logger = logger;
        _esphomeOptions = esphomeOptionsMonitor.CurrentValue;

        InitOption();

        _esphomeOptionsDispose = esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }

    public Subscriber Subscribe(IProcessEventSubscriber sub)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Subscribe Start {Count} {Name}", nameof(ProcessEvent), subscriber.Count, sub);

        var result = subscriber.GetOrAdd(sub, new Subscriber(sub));

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Subscribe Stop {Count} {Name}", nameof(ProcessEvent), subscriber.Count, sub);

        return result;
    }

    public void Unsubscribe(IProcessEventSubscriber sub)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Unsubscribe Start {Count} {Name}", nameof(ProcessEvent), subscriber.Count, sub);

        if (subscriber.TryRemove(sub, out var inst))
        {
            inst.Dispose();
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Unsubscribe Stop {Count} {Name}", nameof(ProcessEvent), subscriber.Count, sub);
    }

    private void OnOptionChanged(EsphomeOptions currentValue)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(ProcessEvent));

        _esphomeOptions = currentValue;

        InitOption();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(ProcessEvent));
    }

    private void InitOption()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption Start", nameof(ProcessEvent));

        DeviceInfo = (from deviceInfo in _esphomeOptions.DeviceInfo
                      from statusInfo in _esphomeOptions.StatusInfo
                      select new
                      {
                          key = string.Concat(statusInfo.Prefix, deviceInfo.Name, statusInfo.Suffix),
                          processOption = new ProcessOption()
                          {
                              DeviceInfo = deviceInfo,
                              StatusInfo = statusInfo,
                              GroupInfo = _esphomeOptions.GroupInfo.FirstOrDefault(x => string.Equals(statusInfo.GroupInfoName, x.Name, StringComparison.OrdinalIgnoreCase))
                          }
                      }).ToFrozenDictionary(k => k.key, v => v.processOption);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption End", nameof(ProcessEvent));
    }

    public FriendlyDisplay TryGetData(string deviceName, string name)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetValue {deviceName} {name}", nameof(ProcessEvent), deviceName, name);

        dataDisplay.TryGetValue((deviceName, name), out var friendlyDisplay);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetValue {shortForm}", nameof(ProcessEvent), friendlyDisplay);

        return friendlyDisplay;
    }

    public decimal? TryGetSumValue(string groupInfo)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetSumValue {groupInfo}", nameof(ProcessEvent), groupInfo);

        var sumValue = dataDisplay.Values
                       .Where(x => string.Equals(x.GroupInfo, groupInfo, StringComparison.OrdinalIgnoreCase))
                       .Sum(x => x.Data);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetSumValue {sumValue}", nameof(ProcessEvent), sumValue);

        return sumValue;
    }

    public async Task SendAsync(EspEvent espEvent, Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventReceived {uri} Start", nameof(ProcessEvent), uri);

        if (DeviceInfo.TryGetValue(espEvent.Id, out var processOption))
        {
            if (!dataDisplay.TryGetValue((processOption.DeviceInfo.DeviceName, processOption.StatusInfo.Name), out var friendlyDisplay))
            {
                friendlyDisplay = AddNewFriendlyDisplay(processOption);
            }

            friendlyDisplay.Data = espEvent.Value.ConvertToDecimal();
            friendlyDisplay.LastUpdate = DateTimeOffset.FromUnixTimeSeconds(espEvent.UnixTime).LocalDateTime;

            await DispatchDataAsync(espEvent, friendlyDisplay);
        }

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventReceived {uri} End", nameof(ProcessEvent), uri);
    }

    private FriendlyDisplay AddNewFriendlyDisplay(ProcessOption processOption)
    {
        FriendlyDisplay friendlyDisplay = new()
        {
            DeviceName = processOption.DeviceInfo.DeviceName,
            Name = processOption.StatusInfo.Name,
            Unit = processOption.StatusInfo.Unit,
            GroupInfo = processOption.StatusInfo.GroupInfoName,
        };

        dataDisplay[(friendlyDisplay.DeviceName, friendlyDisplay.Name)] = friendlyDisplay;

        return friendlyDisplay;
    }

    private async Task DispatchDataAsync(EspEvent espEvent, FriendlyDisplay friendlyDisplay)
    {
        foreach (var sub in subscriber)
        {
            if (sub.Value.EventSingleCanReceives.TryGetValue((friendlyDisplay.DeviceName, friendlyDisplay.Name), out var canReceiveSingle))
            {
                if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventSingleCanReceives ReceiveDataAsync {friendlyDisplay}", nameof(ProcessEvent), friendlyDisplay);

                await canReceiveSingle.ReceiveDataAsync(friendlyDisplay);
            }

            if (!string.IsNullOrEmpty(friendlyDisplay.GroupInfo) && sub.Value.EventGroupCanReceives.TryGetValue(friendlyDisplay.GroupInfo, out var canReceiveGroup))
            {
                if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventGroupCanReceives ReceiveDataAsync {friendlyDisplay}", nameof(ProcessEvent), friendlyDisplay);

                await canReceiveGroup.ReceiveDataAsync(friendlyDisplay);
            }

            var onEvent = sub.Value.OnEvent;
            if (onEvent != null)
            {
                await onEvent.ReceiveRawDataAsync(espEvent);
            }
        }
    }

    public async Task SendAsync(Exception exception, Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} ExceptionReceived {uri} Start", nameof(ProcessEvent), uri);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} ExceptionReceived Invoke {exception}", nameof(ProcessEvent), exception);

        foreach (var sub in subscriber)
        {
            var onEvent = sub.Value.OnEvent;
            if (onEvent != null)
            {
                await onEvent.ReceiveDataAsync(exception, uri);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} ExceptionReceived {uri} Stop", nameof(ProcessEvent), uri);

        await Task.CompletedTask;
    }

    public async Task SendAsync(string data, Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} RawMessageReceived {uri} Start", nameof(ProcessEvent), uri);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} RawMessageReceived Invoke {data}", nameof(ProcessEvent), data);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} RawMessageReceived {uri} Stop", nameof(ProcessEvent), uri);

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(ProcessEvent));

        foreach (var sub in subscriber)
        {
            sub.Value.Dispose();
        }

        subscriber.Clear();

        _esphomeOptionsDispose?.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(ProcessEvent));
    }
}
