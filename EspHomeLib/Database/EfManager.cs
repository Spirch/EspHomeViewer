using EspHomeLib.Database.Model;
using EspHomeLib.Dto;
using EspHomeLib.Interface;
using EspHomeLib.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EspHomeLib.Database;
public class EfManager : IHostedService, IProcessEventSubscriber, IEventCanReceive, IDisposable
{
    private readonly IOptionsMonitor<EsphomeOptions> _esphomeOptionsMonitor;
    private readonly IDisposable _esphomeOptionsDispose;
    private EsphomeOptions _esphomeOptions;

    private readonly ILogger<SseClient> _logger;
    private readonly ProcessEvent _processEvent;
    private readonly EfContext _efContext;
    private readonly BlockingCollection<IDbItem> Queue = new();

    private ConcurrentDictionary<string, RecordData> recordData = new();

    private Subscriber subscriber;
    private Task runningInstance;

    public EfManager(ProcessEvent processEvent, IOptionsMonitor<EsphomeOptions> esphomeOptionsMonitor, EfContext efContext, ILogger<SseClient> logger)
    {
        _efContext = efContext;
        _processEvent = processEvent;
        _esphomeOptionsMonitor = esphomeOptionsMonitor;
        _logger = logger;

        subscriber = _processEvent.Subscribe(this);
        subscriber.EveryRawEvent = this;

        InitOption();

        _esphomeOptionsDispose = _esphomeOptionsMonitor.OnChange(OnOptionChanged);
    }
    private void OnOptionChanged(EsphomeOptions _)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged Start", nameof(EfManager));

        InitOption();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} OnOptionChanged End", nameof(EfManager));
    }
    private void InitOption()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption Start", nameof(EfManager));

        _esphomeOptions = _esphomeOptionsMonitor.CurrentValue;

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} InitOption End", nameof(EfManager));
    }

    public void Dispose()
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose Start", nameof(EfManager));

        _processEvent.Unsubscribe(this);
        _esphomeOptionsDispose?.Dispose();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} Dispose End", nameof(EfManager));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync Start", nameof(EfManager));

        runningInstance = RunAndProcessAsync();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StartAsync End", nameof(EfManager));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync Start", nameof(EfManager));

        Queue.CompleteAdding();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("{Class} StopAsync End", nameof(EfManager));

        return Task.CompletedTask;
    }

    private async Task RunAndProcessAsync()
    {
        while (!Queue.IsCompleted)
        {
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        foreach (var dbItem in Queue.GetConsumingEnumerable())
                        {
                            if (dbItem is Event json)
                            {
                                await GetDescIdAsync(json);
                            }

                            await _efContext.AddAsync(dbItem);
                            await _efContext.SaveChangesAsync();
                            _efContext.ChangeTracker.Clear();
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
        var newEvent = new Event(espEvent);

        recordData.TryGetValue(espEvent.Id, out var data);

        if (espEvent.Event_Type == null && data != null)
        {
            if(!data.LastRecordSw.IsRunning || Math.Abs(espEvent.Data - data.LastValue) >= data.RecordDelta || data.LastRecordSw.Elapsed.TotalSeconds >= data.RecordThrottle)
            {
                if (data.LastValue != espEvent.Data)
                {
                    data.LastValue = espEvent.Data;
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

    private async Task InsertErrorAsync(Error error)
    {
        Queue.Add(error);

        await Task.CompletedTask;
    }

    private async Task GetDescIdAsync(Event json)
    {
        //if(recordData.Count == 0)
        //{
        //    var entries = _efContext.RowEntry.ToList();

        //    foreach (var entry in entries)
        //    {
        //        if (_processEvent.DeviceInfo.TryGetValue(entry.Name, out var deviceInfo))
        //        {
        //            NewRecordData(json, deviceInfo, entry);
        //        }
        //    }
        //}

        if (!recordData.TryGetValue(json.SourceId, out var data))
        {
            if(_processEvent.DeviceInfo.TryGetValue(json.SourceId, out var deviceInfo))
            {
                var rowEntry = _efContext.RowEntry.FirstOrDefault(x => x.Name == json.SourceId);

                if(rowEntry == null)
                {
                    rowEntry = new RowEntry()
                    {
                        FriendlyName = deviceInfo.deviceInfo.DeviceName,
                        Name = json.SourceId,
                        Unit = deviceInfo.statusInfo.Unit,
                    };

                    await _efContext.AddAsync(rowEntry);
                    await _efContext.SaveChangesAsync();
                }

                NewRecordData(json, deviceInfo, rowEntry);
            }
        }

        json.RowEntryId = recordData[json.SourceId].RowEntry.RowEntryId.Value;
    }

    private void NewRecordData(Event json, (DeviceInfoOption deviceInfo, StatusInfoOption statusInfo) deviceInfo, RowEntry rowEntry)
    {
        recordData[json.SourceId] = new()
        {
            RowEntry = rowEntry,
            LastRecordSw = new(),
            RecordDelta = deviceInfo.statusInfo.RecordDelta,
            RecordThrottle = deviceInfo.statusInfo.RecordThrottle,
        };
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

    public async Task<bool> GcCollected(bool alreadyCollected)
    {
        await Task.CompletedTask;

        return false;
    }

    public async Task ReceiveDataAsync(FriendlyDisplay friendlyDisplay)
    {
        await Task.CompletedTask;
    }

    public async Task ReceiveDataAsync(Exception exception)
    {
        await Task.CompletedTask;
    }

    public async Task ReceiveDataAsync(string rawMessage)
    {
        await Task.CompletedTask;
    }
}
