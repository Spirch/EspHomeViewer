using EspHomeLib.Database.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
