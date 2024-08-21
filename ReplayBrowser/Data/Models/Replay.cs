using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using ReplayBrowser.Models;
using ReplayBrowser.Models.Ingested;
using YamlDotNet.Serialization;

namespace ReplayBrowser.Data.Models;

public class Replay : IEntityTypeConfiguration<Replay>
{
    public int Id { get; set; }

    public string Link { get; set; }

    public int? RoundId { get; set; }

    public string? ServerName { get; set; }
    public DateTime? Date { get; set; }

    public string? Map { get; set; }

    public List<string>? Maps { get; set; }
    public string Gamemode { get; set; }
    public List<ReplayParticipant>? RoundParticipants { get; set; }
    public string? RoundEndText { get; set; }
    public string ServerId { get; set; }
    public int EndTick { get; set; }
    public string Duration { get; set; }
    public int FileCount { get; set; }
    public int Size { get; set; }
    public int UncompressedSize { get; set; }
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

    public static Replay FromYaml(YamlReplay replay, string link)
    {
        var participants = replay.RoundEndPlayers?
            .GroupBy(p => p.PlayerGuid)
            .Select(pg => new ReplayParticipant {
                PlayerGuid = pg.Key,
                Players = pg.Select(yp => Player.FromYaml(yp)).ToList()
            })
            .ToList();

        return new Replay {
            Link = link,
            ServerId = replay.ServerId,
            ServerName = replay.ServerName,
            Gamemode = replay.Gamemode,
            Map = replay.Map,
            Maps = replay.Maps,

            RoundParticipants = participants,

            EndTick = replay.EndTick,
            Duration = replay.Duration,
            FileCount = replay.FileCount,
            Size = replay.Size,
            UncompressedSize = replay.UncompressedSize,
            EndTime = replay.EndTime
        };
    }

    public void RedactInformation(Guid? accountGuid, bool wasGdpr)
    {
        if (accountGuid == null)
            return;
        if (RoundParticipants == null)
            return;

        foreach (var participant in RoundParticipants)
        {
            if (participant.PlayerGuid != accountGuid)
                continue;

            // FIXME: This can cause unique constraint failure! Take care when redacting
            participant.PlayerGuid = Guid.Empty;
            if (wasGdpr)
            {
                participant.Username = "Removed by GDPR request";
            }
            else
            {
                participant.Username = "Redacted";
            }

            if (participant.Players is null)
                return;

            // Note that the above might be null if entries were not .Included!
            foreach (var character in participant.Players)
            {
                character.RedactInformation();
            }
        }
    }

    public void RedactCleanup()
    {
        if (RoundParticipants is null) return;
        var empty = RoundParticipants.Where(p => p.PlayerGuid == Guid.Empty).ToList();
        RoundParticipants!.RemoveAll(p => p.PlayerGuid == Guid.Empty);
        RoundParticipants.Add(
            new ReplayParticipant {
                PlayerGuid = Guid.Empty,
                // Collate everything into one more generic group
                Username = "Redacted",
                Players = empty.SelectMany(p => p.Players!).ToList()
            }
        );
    }
}