using EspHomeLib.Dto;
using EspHomeLib.Helper;
using EspHomeLib.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace EspHomeLib;
public class ProcessEvent : IDisposable
{
    private readonly EspHomeData _espHomeData;

    private readonly ILogger<ProcessEvent> _logger;

    private readonly ConcurrentDictionary<IProcessEventSubscriber, Subscriber> subscriber = new();

    public ProcessEvent(EspHomeData espHomeData,
                        ILogger<ProcessEvent> logger)
    {
        _logger = logger;
        _espHomeData = espHomeData;

        _espHomeData.OnEspHomeOptionChanged += OnEspHomeOptionChanged;
    }
    private void OnEspHomeOptionChanged(object? sender, EventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnEspHomeOptionChanged Start", nameof(ProcessEvent));

        //do nothing for now

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnEspHomeOptionChanged End", nameof(ProcessEvent));
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

    public async Task SendAsync(EspEvent espEvent, Uri uri)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventReceived {uri} Start", nameof(ProcessEvent), uri);

        if (_espHomeData.MergeInfo.TryGetValue(espEvent.Id, out var processOption))
        {
            if (_espHomeData.DataDisplay.TryGetValue((processOption.DeviceInfo.DeviceName, processOption.StatusInfo.Name), out var friendlyDisplay))
            {
                friendlyDisplay.Data = espEvent.Value.ConvertToDecimal();
                friendlyDisplay.LastUpdate = DateTimeOffset.FromUnixTimeSeconds(espEvent.UnixTime).LocalDateTime;

                await DispatchDataAsync(espEvent, friendlyDisplay);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} EventReceived {uri} End", nameof(ProcessEvent), uri);
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

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(ProcessEvent));

        foreach (var sub in subscriber)
        {
            sub.Value.Dispose();
        }

        subscriber.Clear();

        _espHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(ProcessEvent));
    }
}
