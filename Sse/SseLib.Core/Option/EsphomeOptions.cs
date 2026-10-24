using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SseLib.Core.Option;

public sealed class EsphomeOptions
{
    public SseClientOption SseClient { get; set; } = new();
    public List<DeviceInfoOption> DeviceInfo { get; set; } = new();
    public List<StatusInfoOption> StatusInfo { get; set; } = new();
    public List<GroupInfoOption> GroupInfo { get; set; } = new();
}
