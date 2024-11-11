using EspHomeLib.Dto;

namespace EspHomeLib.Database.Model;

sealed public class Event : IDbItem
{
    public Event()
    {
        
    }

    public Event(EspEvent espEvent)
    {
        Data = espEvent.Data;
        UnixTime = espEvent.UnixTime;
        SourceId = espEvent.Id;
    }

    public long EventId { get; set; }
    public int RowEntryId { get; set; }
    public decimal? Data { get; set; }
    public long UnixTime { get; set; }

    public RowEntry EspHomeId { get; set; }

    public string SourceId { get; set; }
}
