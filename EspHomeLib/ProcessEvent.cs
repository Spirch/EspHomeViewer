using EspHomeLib.Dto;
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
    private readonly IOptionsMonitor<EsphomeOptions> _esphomeOptionsMonitor;
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly ILogger<ProcessEvent> _logger;

    private readonly ConcurrentDictionary<(string, string), FriendlyDisplay> dataDisplay = new();

    private readonly ConcurrentDictionary<IProcessEventSubscriber, Subscriber> subscriber = new();

    public FrozenDictionary<string, (DeviceInfoOption deviceInfo, StatusInfoOption statusInfo)> DeviceInfo { get; set; }

    public ProcessEvent(IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor, ILogger<ProcessEvent> logger)
    {
        _esphomeOptionsMonitor = esphomeOptionsMonitor;
        _logger = logger;

        InitOption();

        _esphomeOptionsDispose = _esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }

    public Subscriber Subscribe(IProcessEventSubscriber sub)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Subscribe Start {Count}", nameof(ProcessEvent), subscriber.Count);

        var result = subscriber.GetOrAdd(sub, new Subscriber());

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Subscribe Stop {Count}", nameof(ProcessEvent), subscriber.Count);

        return result;
    }

    public void Unsubscribe(IProcessEventSubscriber sub)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Unsubscribe Start {Count}", nameof(ProcessEvent), subscriber.Count);

        subscriber.TryRemove(sub, out var inst);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Unsubscribe Stop {Count}", nameof(ProcessEvent), subscriber.Count);
    }

    private void OnOptionChanged(EsphomeOptions _)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(ProcessEvent));

        InitOption();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(ProcessEvent));
    }

    private void InitOption()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption Start", nameof(ProcessEvent));

        _esphomeOptions = _esphomeOptionsMonitor.CurrentValue;

        DeviceInfo = (from deviceInfo in _esphomeOptions.DeviceInfo
                    from statusInfo in _esphomeOptions.StatusInfo
                    select new
                    {
                        key = string.Concat(statusInfo.Prefix, deviceInfo.Name, statusInfo.Suffix),
                        deviceInfo,
                        statusInfo
                    }).ToFrozenDictionary(k => k.key, v => (v.deviceInfo, v.statusInfo));

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption End", nameof(ProcessEvent));
    }
    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(ProcessEvent));

        _esphomeOptionsDispose?.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(ProcessEvent));
    }

    public FriendlyDisplay TryGetValue(string deviceName, string name)
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

        if (DeviceInfo.TryGetValue(espEvent.Id, out (DeviceInfoOption deviceInfo, StatusInfoOption statusInfo) info))
        {
            if (!dataDisplay.TryGetValue((info.deviceInfo.DeviceName, info.statusInfo.Name), out var friendlyDisplay))
            {
                friendlyDisplay = new()
                {
                    DeviceName = info.deviceInfo.DeviceName,
                    Name = info.statusInfo.Name,
                    Unit = info.statusInfo.Unit,
                    GroupInfo = info.statusInfo.GroupInfo?.Name,
                };

                dataDisplay[(friendlyDisplay.DeviceName, friendlyDisplay.Name)] = friendlyDisplay;
            }

            friendlyDisplay.Data = espEvent.Data;

            bool gcCollected = false;

            foreach(var sub in subscriber)
            {
                if (sub.Value.EventSingleCanReceives.TryGetValue((friendlyDisplay.DeviceName, friendlyDisplay.Name), out var canReceiveSingle))
                {
                    if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventSingleCanReceives ReceiveDataAsync {friendlyDisplay}", nameof(ProcessEvent), friendlyDisplay);

                    await canReceiveSingle.ReceiveDataAsync(friendlyDisplay);
                }

                if (friendlyDisplay.GroupInfo != null && sub.Value.EventGroupCanReceives.TryGetValue(friendlyDisplay.GroupInfo, out var canReceiveGroup))
                {
                    if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventGroupCanReceives ReceiveDataAsync {friendlyDisplay}", nameof(ProcessEvent), friendlyDisplay);

                    await canReceiveGroup.ReceiveDataAsync(friendlyDisplay);
                }

                if(sub.Value.EveryRawEvent != null)
                {
                    await sub.Value.EveryRawEvent.ReceiveRawDataAsync(espEvent);
                }

                gcCollected = await sub.Key.GcCollected(gcCollected);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventReceived {uri} End", nameof(ProcessEvent), uri);
    }

    public async Task SendAsync(Exception exception, Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} ExceptionReceived {uri} Start", nameof(ProcessEvent), uri);

        //if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} ExceptionReceived Invoke {exception}", nameof(ProcessEvent), exception);

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} ExceptionReceived {uri} Stop", nameof(ProcessEvent), uri);

        await Task.CompletedTask;
    }

    public async Task SendAsync(string data, Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} RawMessageReceived {uri} Start", nameof(ProcessEvent), uri);

        //if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} RawMessageReceived Invoke {data}", nameof(ProcessEvent), data);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} RawMessageReceived {uri} Stop", nameof(ProcessEvent), uri);

        await Task.CompletedTask;
    }
}
