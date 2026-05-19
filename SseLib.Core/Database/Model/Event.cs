using SseLib.Core.Dto;
using SseLib.Core.Helper;

namespace SseLib.Core.Database.Model;

public sealed class Event : IDbItem
{
    public Event()
    {

    }

    public Event(EspEvent espEvent)
    {
        Data = espEvent.Value.ConvertToFloat();
        UnixTime = espEvent.UnixTime;
        SourceId = espEvent.Id;
    }

    public long EventId { get; set; }
    public int RowEntryId { get; set; }
    public float Data { get; set; }
    public long UnixTime { get; set; }

    public RowEntry EspHomeId { get; set; }

    public string SourceId { get; set; }
    public bool IsGroup { get; set; }
}
