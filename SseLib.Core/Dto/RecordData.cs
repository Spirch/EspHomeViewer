using SseLib.Core.Database.Model;
using System.Diagnostics;

namespace SseLib.Core.Dto;
public class RecordData
{
    public RowEntry RowEntry { get; set; }

    public float RecordDelta { get; set; }
    public float LastValue { get; set; }

    public int RecordThrottle { get; set; }
    public Stopwatch LastRecordSw { get; set; }

    public string GroupInfoName { get; set; }
}
