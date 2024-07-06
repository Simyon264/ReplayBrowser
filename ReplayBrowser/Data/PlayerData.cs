using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ReplayBrowser.Data;

/// <summary>
/// Represents collected player data from replays. This is used to generate leaderboards and other statistics.
/// </summary>
public class PlayerData
{
    [JsonIgnore]
    public int Id { get; set; }
    
    public Guid? PlayerGuid { get; set; }
    
    public string Username { get; set; }
    

    public override bool Equals(object? obj)
    {
        if (obj is not PlayerData other)
        {
            return false;
        }

        return PlayerGuid == other.PlayerGuid;
    }
    
    public override int GetHashCode()
    {
        return PlayerGuid.GetHashCode();
    }

    public void RedactInformation()
    {
        PlayerGuid = Guid.Empty;
        Username = "Redacted";
    }
}