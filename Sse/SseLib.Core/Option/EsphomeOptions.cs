using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SseLib.Core.Option;

public class EsphomeOptions
{
    [Required]
    public List<Uri> Uri { get; set; } = new();
    public SseClientOption SseClient { get; set; } = new();
    public List<DeviceInfoOption> DeviceInfo { get; set; } = new();
    public List<StatusInfoOption> StatusInfo { get; set; } = new();
    public List<GroupInfoOption> GroupInfo { get; set; } = new();
}
