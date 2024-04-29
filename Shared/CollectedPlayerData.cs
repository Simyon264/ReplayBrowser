namespace Shared;

/// <summary>
/// Represents a player's data over all replays.
/// </summary>
public class CollectedPlayerData
{
    public PlayerData PlayerData { get; init; } = new();

    /// <summary>
    /// Characters played by the player
    /// </summary>
    public List<CharacterData> Characters { get; set; } = new();
    
    /// <summary>
    /// Represents the estimated total playtime of the player. This is calculated by summing the roundtime of all replays the player has played.
    /// </summary>
    public TimeSpan TotalEstimatedPlaytime { get; init; }
    
    /// <summary>
    /// Represents the total amount of rounds the player has played.
    /// </summary>
    public int TotalRoundsPlayed { get; init; }
    
    /// <summary>
    /// Represents the total amount of antag rounds the player has played.
    /// </summary>
    public int TotalAntagRoundsPlayed { get; init; }
    
    public DateTime LastSeen { get; set; } = DateTime.MinValue;
    
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
    public string CharacterName { get; set; }
    public DateTime LastPlayed { get; set; } = DateTime.MinValue;
    public int RoundsPlayed { get; set; }
}