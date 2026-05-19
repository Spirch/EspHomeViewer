using SseLib.Database.Context.Model;
using System;

namespace SseLib.Database.Dto;
public class RecordData
{
    internal long LastRecordUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    public RowEntry RowEntry { get; set; }

    public float RecordDelta { get; set; }
    public float LastValue { get; set; }

    public int RecordThrottle { get; set; }

    public string GroupInfoName { get; set; }
}
