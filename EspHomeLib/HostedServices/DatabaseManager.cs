using EspHomeLib.Database;
using EspHomeLib.Database.Model;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib.HostedServices;
public class DatabaseManager : IHostedService, IProcessEventSubscriber, IEventCanReceive, IDisposable
{
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly ILogger<SseClient> _logger;
    private readonly ProcessEvent _processEvent;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly BlockingCollection<IDbItem> Queue = new();

    private readonly ConcurrentDictionary<string, RecordData> recordData = new();

    private readonly Subscriber subscriber;
    private Task runningInstance;

    public DatabaseManager(ProcessEvent processEvent, 
                           IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor,
                           IServiceScopeFactory serviceScopeFactory,
                           ILogger<SseClient> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _processEvent = processEvent;
        _logger = logger;
        _esphomeOptions = esphomeOptionsMonitor.CurrentValue;

        InitOption();

        subscriber = _processEvent.Subscribe(this);
        subscriber.OnEvent = this;
        
        _esphomeOptionsDispose = esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }

    private void OnOptionChanged(EsphomeOptions currentValue)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(DatabaseManager));

        _esphomeOptions = currentValue;

        InitOption();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(DatabaseManager));
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

        foreach (var device in _esphomeOptions.MergeInfo)
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

        foreach (var group in _esphomeOptions.GroupInfo)
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

        runningInstance = RunAndProcessAsync();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync End", nameof(DatabaseManager));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync Start", nameof(DatabaseManager));

        Queue.CompleteAdding();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync End", nameof(DatabaseManager));

        return Task.CompletedTask;
    }

    private async Task RunAndProcessAsync()
    {
        var swCleanup = Stopwatch.StartNew();

        while (!Queue.IsCompleted)
        {
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        using var efContext = scope.ServiceProvider.GetRequiredService<EfContext>();

                        foreach (var dbItem in Queue.GetConsumingEnumerable())
                        {
                            await efContext.AddAsync(dbItem);
                            await efContext.SaveChangesAsync();
                            efContext.ChangeTracker.Clear();

                            if(swCleanup.Elapsed.TotalHours > 24)
                            {
                                await efContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
                                swCleanup.Restart();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        await HandleErrorAsync(e, "EspHomeContext.RunAndProcess.Run");

                        await Task.Delay(5000);
                    }
                });
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e, "EspHomeContext.RunAndProcess");

                await Task.Delay(5000);
            }
        }
    }

    public async Task ReceiveRawDataAsync(EspEvent espEvent)
    {
        await HandleSingleEvent(espEvent);
        await HandleGroupEvent(espEvent);
    }

    private async Task HandleSingleEvent(EspEvent espEvent)
    {
        var newEvent = new Event(espEvent);

        recordData.TryGetValue(newEvent.SourceId, out var data);
        newEvent.RowEntryId = data.RowEntry.RowEntryId.Value;

        if (espEvent.Event_Type == null)
        {
            if (Math.Abs(newEvent.Data - data.LastValue) >= data.RecordDelta || data.LastRecordSw.Elapsed.TotalSeconds >= data.RecordThrottle)
            {
                if (data.LastValue != newEvent.Data)
                {
                    data.LastValue = newEvent.Data;
                    Queue.Add(newEvent);
                }

                data.LastRecordSw.Restart();
            }
        }
        else
        {
            Queue.Add(newEvent);
        }

        await Task.CompletedTask;
    }

    private async Task HandleGroupEvent(EspEvent espEvent)
    {
        if (_esphomeOptions.MergeInfo.TryGetValue(espEvent.Id, out var processOption) &&
            processOption.GroupInfo != null)
        {
            var newEvent = new Event()
            {
                SourceId = processOption.GroupInfo.Id,
                Data = _processEvent.TryGetSumValue(processOption.GroupInfo.Name) ?? 0m,
                UnixTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                IsGroup = true,
            };

            recordData.TryGetValue(newEvent.SourceId, out var data);
            newEvent.RowEntryId = data.RowEntry.RowEntryId.Value;

            if (data.LastRecordSw.Elapsed.TotalSeconds >= data.RecordThrottle)
            {
                Queue.Add(newEvent);

                data.LastRecordSw.Restart();
            }
        }

        await Task.CompletedTask;
    }

    private async Task InsertErrorAsync(Error error)
    {
        Queue.Add(error);

        await Task.CompletedTask;
    }

    public async Task ReceiveDataAsync(Exception exception, Uri uri)
    {
        await HandleErrorAsync(exception, uri.ToString());
        await Task.CompletedTask;
    }

    public async Task HandleErrorAsync(Exception e, string source, string message = null)
    {
        await InsertErrorAsync(new Error()
        {
            Date = DateTime.Now.ToString(_esphomeOptions.SseClient.DateTimeFormat),
            DeviceName = source,
            Exception = e.ToString(),
            Message = message ?? e.Message
        });
    }

    public async Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay)
    {
        await Task.CompletedTask;
    }

    public async Task ReceiveDataAsync(string rawMessage)
    {
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(DatabaseManager));

        _processEvent.Unsubscribe(this);
        _esphomeOptionsDispose?.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(DatabaseManager));
    }
}
