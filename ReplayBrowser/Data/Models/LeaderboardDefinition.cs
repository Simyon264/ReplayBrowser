using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReplayBrowser.Data.Models;

public class LeaderboardDefinition  : IEntityTypeConfiguration<LeaderboardDefinition>
{
    public required string Name { get; set; }

    /// <summary>
    /// The text that will appear for the "Count" column.
    /// </summary>
    public required string TrackedData { get; set; }

    /// <summary>
    /// Will be displayed in a small font below the name.
    /// </summary>
    public string? ExtraInfo { get; set; }
    public string NameColumn { get; set; } = "Player Name";

    public void Configure(EntityTypeBuilder<LeaderboardDefinition> builder)
    {
        builder.HasKey(x => x.Name);
        builder.Property(x => x.TrackedData).IsRequired();
        builder.Property(x => x.NameColumn).IsRequired();
    }
}