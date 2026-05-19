using SseLib.Database.Context.Model;
using System.Diagnostics;

namespace SseLib.Database.Dto;
public class RecordData
{
    public RowEntry RowEntry { get; set; }

    public float RecordDelta { get; set; }
    public float LastValue { get; set; }

    public int RecordThrottle { get; set; }
    public Stopwatch LastRecordSw { get; set; }

    public string GroupInfoName { get; set; }
}
