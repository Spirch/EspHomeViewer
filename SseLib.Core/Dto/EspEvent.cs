using SseLib.Core.Helper;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SseLib.Core.Dto;

public class EspEvent
{
    public override string ToString()
    {
        return $"Id: {Id}, Value: {Value}, Name: {Name}, State: {State}, Event_Type: {Event_Type}, FloatValue: {Value.ConvertToFloat():0.##}, UnixTimeMs: {UnixTime}";
    }

    //sse event fields

    public string Id { get; set; }
    public JsonElement? Value { get; set; }
    public string Name { get; set; }
    public string State { get; set; }
    public string Event_Type { get; set; }


    // custom fields from this point

    [JsonIgnore]
    public long UnixTime { get; set; }
}
