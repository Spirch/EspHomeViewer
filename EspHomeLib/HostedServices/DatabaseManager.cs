using ChannelLib;
using EspHomeLib.Database;
using EspHomeLib.Database.Model;
using EspHomeLib.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib.HostedServices;

public class DatabaseManager : IHostedService, IChannelSubscriber, IDisposable
{
    private readonly EspHomeData _espHomeData;

    private readonly ILogger<DatabaseManager> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly EventSubscriber<EspEvent> eventSubscriberEspEvent;
    private readonly EventSubscriber<Exception> eventSubscriberException;
    private readonly CancellationTokenSource eventSubscriberCT = new();

    private readonly ConcurrentDictionary<string, RecordData> recordData = new();

    private readonly ConcurrentQueue<IDbItem> record = new();

    public string ChannelNameId => nameof(DatabaseManager);

    public DatabaseManager(EventBroadcaster<IChannelSubscriber, EspEvent> channelSubscriberEspEvent,
                           EventBroadcaster<IChannelSubscriber, Exception> channelSubscriberException,
                           EspHomeData espHomeData,
                           IServiceScopeFactory serviceScopeFactory,
                           ILogger<DatabaseManager> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _espHomeData = espHomeData;

        InitOption();

        eventSubscriberEspEvent = channelSubscriberEspEvent.Subscribe(this);
        eventSubscriberException = channelSubscriberException.Subscribe(this);
        SubscriberReader();

        _espHomeData.OnEspHomeOptionChanged += OnEspHomeOptionChanged;
    }

    private void OnEspHomeOptionChanged(object? sender, EventArgs e)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnEspHomeOptionChanged Start", nameof(DatabaseManager));

        InitOption();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnEspHomeOptionChanged End", nameof(DatabaseManager));
    }

    private void InitOption()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption Start", nameof(DatabaseManager));

        InitRecordData();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption End", nameof(DatabaseManager));
    }

    private void InitRecordData()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        using var efContext = scope.ServiceProvider.GetRequiredService<EfContext>();

        foreach (var device in _espHomeData.MergeInfo)
        {
            RowEntry rowEntry = efContext.RowEntry
                                            .FirstOrDefault(x => x.Name == device.Key &&
                                                                x.FriendlyName == device.Value.DeviceInfo.DeviceName);

            if (rowEntry == null)
            {
                rowEntry = new RowEntry()
                {
                    Name = device.Key,
                    FriendlyName = device.Value.DeviceInfo.DeviceName,
                    Unit = device.Value.StatusInfo.Unit,
                };

                efContext.Add(rowEntry);
            }
            else
            {
                rowEntry.FriendlyName = device.Value.DeviceInfo.DeviceName;
                rowEntry.Unit = device.Value.StatusInfo.Unit;
            }

            if (!recordData.TryGetValue(rowEntry.Name, out var data))
            {
                data = new()
                {
                    LastRecordSw = Stopwatch.StartNew(),
                };

                recordData[rowEntry.Name] = data;
            }

            data.RowEntry = rowEntry;
            data.RecordDelta = device.Value.StatusInfo.RecordDelta;
            data.RecordThrottle = device.Value.StatusInfo.RecordThrottle;
            data.GroupInfoName = device.Value.StatusInfo.GroupInfoName;
        }

        foreach (var group in _espHomeData.EsphomeOptions.GroupInfo)
        {
            RowEntry rowEntry = efContext.RowEntry.FirstOrDefault(x => x.Name == group.Id);

            if (rowEntry == null)
            {
                rowEntry = new RowEntry()
                {
                    Name = group.Id,
                    FriendlyName = group.Title,
                    Unit = group.Unit,
                };

                efContext.Add(rowEntry);
            }
            else
            {
                rowEntry.FriendlyName = group.Title;
                rowEntry.Unit = group.Unit;
            }

            if (!recordData.TryGetValue(rowEntry.Name, out var data))
            {
                data = new()
                {
                    LastRecordSw = Stopwatch.StartNew(),
                };

                recordData[rowEntry.Name] = data;
            }

            data.RowEntry = rowEntry;
            data.RecordThrottle = group.RecordThrottle;
        }

        efContext.SaveChanges();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync Start", nameof(DatabaseManager));

        DealWithDb();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync End", nameof(DatabaseManager));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync Start", nameof(DatabaseManager));

        eventSubscriberCT.Cancel();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync End", nameof(DatabaseManager));

        return Task.CompletedTask;
    }

    private void DealWithDb()
    {
        _ = Task.Run(async () =>
        {
            while(!eventSubscriberCT.IsCancellationRequested)
            {
                try
                {
                    var swCleanup = Stopwatch.StartNew();
                    using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));

                    while (await timer.WaitForNextTickAsync(eventSubscriberCT.Token))
                    {
                        if (record.IsEmpty)
                        {
                            continue;
                        }

                        using var scope = _serviceScopeFactory.CreateScope();
                        using var efContext = scope.ServiceProvider.GetRequiredService<EfContext>();

                        while (record.TryDequeue(out var item))
                        {
                            efContext.Add(item);
                        }

                        await efContext.SaveChangesAsync();

                        if (swCleanup.Elapsed.TotalHours > 6)
                        {
                            await efContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
                            swCleanup.Restart();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "DealWithDb");
                    await Task.Delay(10000);
                }
            }
        }, eventSubscriberCT.Token);
    }

    private void SubscriberReader()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var espEvent in eventSubscriberEspEvent.Reader.ReadAllAsync(eventSubscriberCT.Token))
                {
                    HandleSingleEvent(espEvent);
                    HandleGroupEvent(espEvent);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, eventSubscriberCT.Token);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var ex in eventSubscriberException.Reader.ReadAllAsync(eventSubscriberCT.Token))
                {
                    HandleError(ex);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, eventSubscriberCT.Token);
    }

    private void HandleSingleEvent(EspEvent espEvent)
    {
        var newEvent = new Event(espEvent);

        if (recordData.TryGetValue(newEvent.SourceId, out var data))
        {
            newEvent.RowEntryId = data.RowEntry.RowEntryId.Value;

            if (espEvent.Event_Type == null)
            {
                if (Math.Abs(newEvent.Data - data.LastValue) >= data.RecordDelta || data.LastRecordSw.Elapsed.TotalSeconds >= data.RecordThrottle)
                {
                    if (data.LastValue != newEvent.Data)
                    {
                        data.LastValue = newEvent.Data;
                        record.Enqueue(newEvent);
                    }

                    data.LastRecordSw.Restart();
                }
            }
            else
            {
                record.Enqueue(newEvent);
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} HandleSingleEvent missing {SourceId}", nameof(DatabaseManager), newEvent.SourceId);
        }
    }

    private void HandleGroupEvent(EspEvent espEvent)
    {
        if (_espHomeData.MergeInfo.TryGetValue(espEvent.Id, out var processOption) &&
            processOption.GroupInfo != null)
        {
            var newEvent = new Event()
            {
                SourceId = processOption.GroupInfo.Id,
                Data = _espHomeData.TryGetSumValue(processOption.GroupInfo.Name) ?? 0m,
                UnixTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                IsGroup = true,
            };

            if (recordData.TryGetValue(newEvent.SourceId, out var data))
            {
                newEvent.RowEntryId = data.RowEntry.RowEntryId.Value;

                if (data.LastRecordSw.Elapsed.TotalSeconds >= data.RecordThrottle)
                {
                    record.Enqueue(newEvent);

                    data.LastRecordSw.Restart();
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} HandleGroupEvent missing {SourceId}", nameof(DatabaseManager), newEvent.SourceId);
            }
        }
    }

    private void HandleError(Exception e)
    {
        string data = string.Empty;
        if (e.Data?.Count > 0)
        {
            foreach (DictionaryEntry d in e.Data)
            {
                data += $"{d.Key}:{d.Value} ";
            }
        }

        record.Enqueue(new Error()
        {
            Date = DateTime.Now.ToString(_espHomeData.EsphomeOptions.SseClient.DateTimeFormat),
            DeviceName = data,
            Exception = e.ToString(),
            Message = e.Message
        });
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(DatabaseManager));

        eventSubscriberCT?.Cancel();
        eventSubscriberEspEvent.Dispose();
        eventSubscriberException.Dispose();
        _espHomeData.OnEspHomeOptionChanged -= OnEspHomeOptionChanged;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(DatabaseManager));
    }
}
