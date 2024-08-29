using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace ReplayBrowser.Models.Ingested.ReplayEvents;

public class ReplayEvent
{
    /// <summary>
    /// Represents the seconds since the start of the round when the event occurred.
    /// </summary>
    public double? Time { get; set; }

    /// <summary>
    /// How severe the event is.
    /// </summary>
    public ReplayEventSeverity Severity { get; set; }

    /// <summary>
    /// The type of event that occurred.
    /// </summary>
    [JsonIgnore] // This is not needed in the JSON.
    public ReplayEventType EventType { get; set; }

    /// <summary>
    /// The type of event that occurred. For serialization purposes.
    /// </summary>
    [NotMapped]
    [YamlIgnore]
    public string EventTypeString
    {
        get => EventType.ToString();
        set => EventType = Enum.Parse<ReplayEventType>(value);
    }

    /// <summary>
    /// The nearest beacon to the event.
    /// </summary>
    public string? NearestBeacon { get; set; }

    /// <summary>
    /// The exact position of the event.
    /// </summary>
    [YamlIgnore]
    public Vector2 Position { get; set; }

    /// <summary>
    /// The exact position of the event. This is needed because Robust stores null vecotrs as 0,0 and also YAML cannot parse string to Vector2.
    /// </summary>
    [NotMapped] // This is not a database column.
    [YamlMember(Alias = "position")]
    public string PositionString
    {
        get => $"{Position.X},{Position.Y}";
        set
        {
            var parts = value.Split(',');
            Position = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
        }
    }
}