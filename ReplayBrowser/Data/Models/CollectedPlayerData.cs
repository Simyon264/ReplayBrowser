using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReplayBrowser.Data.Models;

/// <summary>
/// Represents a player's data over all replays.
/// </summary>
public class CollectedPlayerData : IEntityTypeConfiguration<CollectedPlayerData>
{
    [JsonIgnore]
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public Guid PlayerGuid { get; set; }

    public PlayerData PlayerData { get; set; } = new();

    /// <summary>
    /// Characters played by the player
    /// </summary>
    public List<CharacterData> Characters { get; set; } = new();

    /// <summary>
    /// Represents the estimated total playtime of the player. This is calculated by summing the roundtime of all replays the player has played.
    /// </summary>
    public TimeSpan TotalEstimatedPlaytime { get; set; }

    /// <summary>
    /// Represents the total amount of rounds the player has played.
    /// </summary>
    public int TotalRoundsPlayed { get; set; }

    /// <summary>
    /// Represents the total amount of antag rounds the player has played.
    /// </summary>
    public int TotalAntagRoundsPlayed { get; set; }

    public List<JobCountData> JobCount { get; set; } = new();

    public DateTime LastSeen { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Is this profile currently being "watched" by the user?
    /// </summary>
    public bool IsWatched { get; set; } = false;

    public void Configure(EntityTypeBuilder<CollectedPlayerData> builder)
    {
        builder.HasKey(p => p.PlayerGuid);
        builder.HasIndex(p => p.PlayerGuid).IsUnique();
    }


    public override bool Equals(object? obj)
    {
        if (obj is not CollectedPlayerData other)
        {
            return false;
        }

        return PlayerData.Equals(other.PlayerData);
    }

    public override int GetHashCode()
    {
        return PlayerData.GetHashCode();
    }
}

public class CharacterData
{
    public int Id { get; set; }
    public required string CharacterName { get; set; }
    public DateTime LastPlayed { get; set; } = DateTime.MinValue;
    public int RoundsPlayed { get; set; }
}

public class JobCountData
{
    public int Id { get; set; }

    public required string JobPrototype { get; set; }
    public int RoundsPlayed { get; set; }
    public DateTime LastPlayed { get; set; } = DateTime.MinValue;
}