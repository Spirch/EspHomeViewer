using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;
using EspHomeLib.Dto;
using System.Collections.Concurrent;

namespace EspHomeLib.Option;
public class EsphomeOptions
{
    public List<Uri> Uri { get; set; }
    public SseClientOption SseClient { get; set; }
    public List<DeviceInfoOption> DeviceInfo { get; set; }
    public List<StatusInfoOption> StatusInfo { get; set; }
    public List<GroupInfoOption> GroupInfo { get; set; }


    private FrozenDictionary<string, ProcessOption> _mergeInfo;

    [JsonIgnore]
    public FrozenDictionary<string, ProcessOption> MergeInfo 
    {
        get
        {
            return _mergeInfo ??= (from deviceInfo in DeviceInfo
                                   from statusInfo in StatusInfo
                                   select new
                                   {
                                       key = string.Concat(statusInfo.Prefix, deviceInfo.Name, statusInfo.Suffix),
                                       processOption = new ProcessOption()
                                       {
                                           DeviceInfo = deviceInfo,
                                           StatusInfo = statusInfo,
                                           GroupInfo = GroupInfo.FirstOrDefault(x => string.Equals(statusInfo.GroupInfoName, x.Name, StringComparison.OrdinalIgnoreCase))
                                       }
                                   }).ToFrozenDictionary(k => k.key, v => v.processOption);
        }
    }

    private ConcurrentDictionary<(string, string), FriendlyDisplay> _dataDisplay;

    [JsonIgnore]
    public ConcurrentDictionary<(string, string), FriendlyDisplay> DataDisplay
    { 
        get
        {
            return _dataDisplay ??= new(MergeInfo.Select(x => new FriendlyDisplay()
                                                 {
                                                     DeviceName = x.Value.DeviceInfo.DeviceName,
                                                     Name = x.Value.StatusInfo.Name,
                                                     Unit = x.Value.StatusInfo.Unit,
                                                     GroupInfo = x.Value.DeviceInfo.IgnoreGroup ? null : x.Value.StatusInfo.GroupInfoName,
                                                 })
                                                 .ToDictionary(k => (k.DeviceName, k.Name)));
        }
    }
}
