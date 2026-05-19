using System;
using System.Collections.Generic;

namespace SseLib.Core.Option;

public class EsphomeOptions
{
    public List<Uri> Uri { get; set; }
    public SseClientOption SseClient { get; set; }
    public List<DeviceInfoOption> DeviceInfo { get; set; }
    public List<StatusInfoOption> StatusInfo { get; set; }
    public List<GroupInfoOption> GroupInfo { get; set; }
}
