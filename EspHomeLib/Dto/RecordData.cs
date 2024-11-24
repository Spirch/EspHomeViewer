using EspHomeLib.Database.Model;
using System.Diagnostics;

namespace EspHomeLib.Dto;
public class RecordData
{
    public RowEntry RowEntry { get; set; }

    public decimal RecordDelta { get; set; }
    public decimal LastValue { get; set; }

    public int RecordThrottle { get; set; }
    public Stopwatch LastRecordSw { get; set; }

    public string GroupInfoName { get; set; }
}
