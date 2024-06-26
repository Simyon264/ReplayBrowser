﻿using System.Text.Json.Serialization;

namespace ReplayBrowser.Data.Models.Account;

/// <summary>
/// Represents the settings for an account.
/// </summary>
public class AccountSettings : ICloneable
{
    [JsonIgnore]
    public int Id { get; set; }
    
    /// <summary>
    /// Specifies whether information inside replays should be redacted.
    /// For example, if you set this to true, the names of this account along with Guid will be replaced with "Redacted".
    /// This account will also not show up in the leaderboards.
    /// </summary>
    public bool RedactInformation { get; set; } = false;
    
    /// <summary>
    /// Which users are allowed to view redacted information?
    /// </summary>
    public List<Guid> Friends { get; set; } = new();

    public object Clone()
    {
        return new AccountSettings
        {
            RedactInformation = RedactInformation,
            Friends = new List<Guid>(Friends)
        };
    }
}