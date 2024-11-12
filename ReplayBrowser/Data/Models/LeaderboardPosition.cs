using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Models;

namespace ReplayBrowser.Data.Models;

public class LeaderboardPosition : IEntityTypeConfiguration<LeaderboardPosition>
{
    public int Id { get; set; }

    /// <summary>
    /// The servers that this position is for.
    /// </summary>
    public required List<string> Servers { get; set; } = null!;
    public required RangeOption RangeOption { get; set; } = RangeOption.AllTime;

    /// <summary>
    /// The number next to the position. For example "most times played" would be a count of how many times the player has played.
    /// </summary>
    public required int Count { get; set; }

    /// <summary>
    /// The position of the player in the leaderboard.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// The GUID of the player. If null, position is not for a player, but rather a general statistic.
    /// </summary>
    public Guid? PlayerGuid { get; set; }

    /// <summary>
    /// The display value of the player or statistic.
    /// </summary>
    public string Username { get; set; } = null!;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public string LeaderboardDefinitionName { get; set; } = null!;
    public LeaderboardDefinition LeaderboardDefinition { get; set; } = null!;

    public void Configure(EntityTypeBuilder<LeaderboardPosition> builder)
    {
        builder.HasOne(lp => lp.LeaderboardDefinition)
            .WithMany()
            .HasForeignKey(lp => lp.LeaderboardDefinitionName);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Servers)
            .IsRequired();

        builder.Property(x => x.Count).IsRequired();
        builder.Property(x => x.Position).IsRequired();
        builder.Property(x => x.PlayerGuid).IsRequired(false);
        builder.Property(x => x.Username).IsRequired();
        builder.Property(x => x.GeneratedAt).IsRequired();
        builder.Property(x => x.RangeOption).IsRequired();

        builder.HasIndex(x => x.Servers);
        builder.HasIndex(x => x.LeaderboardDefinitionName);
        builder.HasIndex(x => x.GeneratedAt);
        builder.HasIndex(x => x.RangeOption);
    }
}