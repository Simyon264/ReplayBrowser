using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace ReplayBrowser.Models.Ingested.ReplayEvents;

/// <summary>
/// Represents a player in a replay event.
/// </summary>
public class ReplayEventPlayer
{
    /// <summary>
    /// The username of the player.
    /// </summary>
    public string? PlayerOocName { get; set; }

    /// <summary>
    /// The character name of the entity the player is playing as.
    /// </summary>
    public string? PlayerIcName { get; set; }

    /// <summary>
    /// The GUID of the player. Null if this was not a connected player who caused the event. (e.g. a NPC)
    /// </summary>
    public Guid? PlayerGuid { get; set; }

    /// <summary>
    /// The job(s) the player was playing as when the event occurred.
    /// </summary>
    public string[]? JobPrototypes { get; set; }

    /// <summary>
    /// The antag role(s) the player was playing as when the event occurred.
    /// </summary>
    public string[]? AntagPrototypes { get; set; }
}