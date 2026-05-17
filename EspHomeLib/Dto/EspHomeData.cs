using ChannelLib;
using EspHomeLib.Helper;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;

namespace EspHomeLib.Dto;

/// <summary>
/// Singleton to hold esphome options and data
/// </summary>
public class EspHomeData : IDisposable
{
    private readonly IDisposable _esphomeOptionsDispose;
    private readonly ILogger<EspHomeData> _logger;
    private readonly SemaphoreSlim handleOnOptionChanged = new(1, 1);
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly EventBroadcaster<IChannelSubscriber<string>, EspEvent> _channelSubscriberEspEvent;
    private readonly EventBroadcaster<IChannelSubscriber<string>, IEspHomeUpdate> _channelSubscriberUpdate;
    private int _disposed; // 0 = false, 1 = true

    private record EspHomeSnapshot(FrozenDictionary<string, ProcessOption> MergeInfo,
                                   ConcurrentDictionary<(string, string), FriendlyDisplay> DataDisplay,
                                   EsphomeOptions Options);

    private volatile EspHomeSnapshot _snapshot;

    public EspHomeData(IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor,
                       EventBroadcaster<IChannelSubscriber<string>, EspEvent> channelSubscriber,
                       EventBroadcaster<IChannelSubscriber<string>, IEspHomeUpdate> channelSubscriberUpdate,
                       ILogger<EspHomeData> logger)
    {
        _logger = logger;
        _channelSubscriberEspEvent = channelSubscriber;
        _channelSubscriberUpdate = channelSubscriberUpdate;

        RefreshFrozenDictionary(esphomeOptionsMonitor.CurrentValue);

        _esphomeOptionsDispose = esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }

    /// <summary>
    /// Dont keep a reference of this, read from this instance only
    /// </summary>
    public FrozenDictionary<string, ProcessOption> MergeInfo => _snapshot?.MergeInfo;

    /// <summary>
    /// Dont keep a reference of this, read from this instance only
    /// External caller can change values
    /// </summary>
    public ConcurrentDictionary<(string, string), FriendlyDisplay> DataDisplay => _snapshot?.DataDisplay;

    /// <summary>
    /// Dont keep a reference of this, read from this instance only
    /// </summary>
    public EsphomeOptions EsphomeOptions => _snapshot?.Options;

    public event EventHandler OnEspHomeOptionChanged = delegate { };

    private void OnOptionChanged(EsphomeOptions currentValue)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(EspHomeData));

        bool waitAcquired = false;

        try
        {
            handleOnOptionChanged.Wait(cancellationTokenSource.Token);
            waitAcquired = true;

            RefreshFrozenDictionary(currentValue);

            try
            {
                OnEspHomeOptionChanged(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Class} OnOptionChanged OnEspHomeOptionChanged Exception", nameof(EspHomeData));
            }
        }
        catch (OperationCanceledException)
        {
            // do nothing
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "{Class} OnOptionChanged failed — invalid configuration", nameof(EspHomeData));
            throw;
        }
        finally
        {
            if (waitAcquired && Volatile.Read(ref _disposed) == 0)
            {
                try
                {
                    handleOnOptionChanged.Release();
                }
                catch (ObjectDisposedException)
                {
                    // do nothing
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(EspHomeData));
    }

    public FriendlyDisplay TryGetData(string deviceName, string name)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetValue {deviceName} {name}", nameof(EspHomeData), deviceName, name);

        DataDisplay.TryGetValue((deviceName, name), out var friendlyDisplay);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetValue {shortForm}", nameof(EspHomeData), friendlyDisplay);

        return friendlyDisplay;
    }

    public decimal? TryGetSumValue(string groupInfo)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetSumValue {groupInfo}", nameof(EspHomeData), groupInfo);

        var sumValue = DataDisplay.Values
                       .Where(x => string.Equals(x.GroupInfo, groupInfo, StringComparison.OrdinalIgnoreCase))
                       .Sum(x => x.Data);

        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("{Class} TryGetSumValue {sumValue}", nameof(EspHomeData), sumValue);

        return sumValue;
    }

    public void UpdateData(EspEvent espEvent)
    {
        espEvent.UnixTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        _channelSubscriberEspEvent.Broadcast(espEvent);

        var snapshot = _snapshot;
        if (snapshot.MergeInfo.TryGetValue(espEvent.Id, out var processOption) &&
            snapshot.DataDisplay.TryGetValue((processOption.DeviceInfo.DeviceName, processOption.StatusInfo.Name), out var friendlyDisplay))
        {
            friendlyDisplay.Data = espEvent.Value.ConvertToDecimal();
            friendlyDisplay.LastUpdate = DateTimeOffset.FromUnixTimeSeconds(espEvent.UnixTime).LocalDateTime;

            _channelSubscriberUpdate.BroadcastByName($"{friendlyDisplay.DeviceName}.{friendlyDisplay.Name}", null);
            if (!string.IsNullOrEmpty(friendlyDisplay.GroupInfo))
            {
                _channelSubscriberUpdate.BroadcastByName(friendlyDisplay.GroupInfo, null);
            }
        }
    }

    private void RefreshFrozenDictionary(EsphomeOptions currentValue)
    {
        //cartesian on purpose
        var mergeInfo = (from deviceInfo in currentValue.DeviceInfo
                         from statusInfo in currentValue.StatusInfo
                         select new
                         {
                             key = string.Concat(statusInfo.Prefix, deviceInfo.Name, statusInfo.Suffix),
                             processOption = new ProcessOption()
                             {
                                 DeviceInfo = deviceInfo,
                                 StatusInfo = statusInfo,
                                 GroupInfo = currentValue.GroupInfo.FirstOrDefault(x => string.Equals(statusInfo.GroupInfoName, x.Name, StringComparison.OrdinalIgnoreCase))
                             }
                         }).ToFrozenDictionary(k => k.key, v => v.processOption);

        var dataDisplay = mergeInfo.Select(x => new FriendlyDisplay()
        {
            DeviceName = x.Value.DeviceInfo.DeviceName,
            Name = x.Value.StatusInfo.Name,
            Unit = x.Value.StatusInfo.Unit,
            GroupInfo = x.Value.DeviceInfo.IgnoreGroup ? null : x.Value.StatusInfo.GroupInfoName,
        })
                                    .ToDictionary(k => (k.DeviceName, k.Name));

        var existingDisplay = _snapshot?.DataDisplay;
        if (existingDisplay != null)
        {
            foreach (var values in existingDisplay)
            {
                if (dataDisplay.TryGetValue(values.Key, out var value))
                {
                    value.Data = values.Value.Data;
                    value.LastUpdate = values.Value.LastUpdate;
                }
            }
        }

        _snapshot = new EspHomeSnapshot(mergeInfo, new(dataDisplay), currentValue);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return; // idempotent
        }

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(EspHomeData));

        cancellationTokenSource.Cancel();

        _esphomeOptionsDispose?.Dispose();
        handleOnOptionChanged.Dispose();
        cancellationTokenSource.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(EspHomeData));
    }
}
