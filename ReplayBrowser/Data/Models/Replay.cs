using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using ReplayBrowser.Models;
using YamlDotNet.Serialization;

namespace ReplayBrowser.Data.Models;

public class Replay : IEntityTypeConfiguration<Replay>
{
    public int Id { get; set; }

    public string? Link { get; set; }

    [YamlMember(Alias = "roundId")]
    public int? RoundId { get; set; }

    [YamlMember(Alias = "server_name")]
    public string? ServerName { get; set; }
    public DateTime? Date { get; set; }

    [YamlMember(Alias = "map")]
    public string? Map { get; set; }

    [YamlMember(Alias = "maps")]
    public List<string>? Maps { get; set; }
    [YamlMember(Alias = "gamemode")]
    public string Gamemode { get; set; }
    [YamlMember(Alias = "roundEndPlayers")]
    public List<Player>? RoundEndPlayers { get; set; }
    [YamlMember(Alias = "roundEndText")]
    public string? RoundEndText { get; set; }
    [YamlMember(Alias = "server_id")]
    public string ServerId { get; set; }
    [YamlMember(Alias = "endTick")]
    public int EndTick { get; set; }
    [YamlMember(Alias = "duration")]
    public string Duration { get; set; }
    [YamlMember(Alias = "fileCount")]
    public int FileCount { get; set; }
    [YamlMember(Alias = "size")]
    public int Size { get; set; }
    [YamlMember(Alias = "uncompressedSize")]
    public int UncompressedSize { get; set; }
    [YamlMember(Alias = "endTime")]
    public string EndTime { get; set; }

    [JsonIgnore]
    public NpgsqlTsVector RoundEndTextSearchVector { get; set; }

    /// <summary>
    /// Determines if a replay is marked as a favorite.
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public bool IsFavorite { get; set; }

    #region Extended Properties

    // None yet.

    #endregion

    public void Configure(EntityTypeBuilder<Replay> builder)
    {
        builder.HasIndex(r => r.Map);
        builder.HasIndex(r => r.Gamemode);
        builder.HasIndex(r => r.ServerId);
        builder.HasIndex(r => r.ServerName);

        builder.HasGeneratedTsVectorColumn(
                p => p.RoundEndTextSearchVector,
                "english",
                r => new { r.RoundEndText }
            )
            .HasIndex(r => r.RoundEndTextSearchVector)
            .HasMethod("GIN");
    }

    public ReplayResult ToResult()
    {
        return new ReplayResult {
            Id = Id,
            Link = Link,
            ServerId = ServerId,
            ServerName = ServerName,
            Gamemode = Gamemode,
            Map = Map,
            Maps = Maps,

            Duration = Duration,
            Date = Date,
            RoundId = RoundId,
            Size = Size,
            UncompressedSize = UncompressedSize
        };
    }

    public void RedactInformation(Guid? accountGuid)
    {
        if (accountGuid == null)
        {
            return;
        }

        if (RoundEndPlayers != null)
        {
            foreach (var player in RoundEndPlayers)
            {
                if (player.PlayerGuid == accountGuid)
                {
                    player.PlayerOocName = "Redacted by user request";
                    player.PlayerGuid = Guid.Empty;
                }
            }
        }
    }
}