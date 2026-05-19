using System.Collections.Generic;

namespace SseLib.Core.Database.Model;

public sealed class RowEntry : IDbItem
{
    public int? RowEntryId { get; set; }
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public string Unit { get; set; }

    public ICollection<Event> Event { get; set; }
}
