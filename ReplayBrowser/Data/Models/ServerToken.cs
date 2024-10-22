using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReplayBrowser.Data.Models;

/// <summary>
/// Represents a token that is used to authenticate with the API for ingesting replays.
/// </summary>
public class ServerToken : IEntityTypeConfiguration<ServerToken>
{
    public required string Token { get; set; }

    public void Configure(EntityTypeBuilder<ServerToken> builder)
    {
        builder.HasKey(t => t.Token);
        builder.HasIndex(t => t.Token);
    }
}