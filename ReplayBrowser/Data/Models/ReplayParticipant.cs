using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReplayBrowser.Data.Models;

/// <summary>
/// Basic entry that says some player was in a given round
/// </summary>
public class ReplayParticipant : IEntityTypeConfiguration<ReplayParticipant>
{
    [JsonIgnore]
    public int Id { get; set; }
    public Guid PlayerGuid { get; set; }
    public string Username { get; set; } = null!;

    [JsonIgnore]
    public Replay Replay { get; set; } = null!;
    [JsonIgnore]
    public int ReplayId { get; set; }

    public List<Player>? Players { get; set; }

    public void Configure(EntityTypeBuilder<ReplayParticipant> builder)
    {
        builder.HasIndex(p => new { p.PlayerGuid, p.ReplayId }).IsUnique();
        builder.HasIndex(p => p.Username);
    }
}