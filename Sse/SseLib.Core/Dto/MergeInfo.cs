using SseLib.Core.Option;

namespace SseLib.Core.Dto;

public sealed class MergeInfo
{
    public DeviceInfoOption DeviceInfo { get; set; }
    public StatusInfoOption StatusInfo { get; set; }
    public GroupInfoOption GroupInfo { get; set; }
}
