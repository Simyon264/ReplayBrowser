using System.Numerics;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Models.Ingested.ReplayEvents;
using ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

namespace ReplayBrowser.Data.Models;

public class ReplayDbEvent : ReplayEvent, IEntityTypeConfiguration<ReplayDbEvent>
{
    [JsonIgnore]
    public int Id { get; set; }

    [JsonIgnore]
    public int ReplayId { get; set; }

    [JsonIgnore]
    public Replay Replay { get; set; } = null!;

    public void Configure(EntityTypeBuilder<ReplayDbEvent> builder)
    {
        //var eventTypes = Assembly.GetExecutingAssembly()
        //    .GetTypes()
        //    .Where(t => t.IsSubclassOf(typeof(ReplayEvent)))
        //    .ToDictionary(t => t.Name, t => t);

        builder.HasDiscriminator<string>("ClassType")
            .HasValue<ReplayDbEvent>(nameof(ReplayDbEvent))
            .HasValue<AlertLevelChangedReplayEvent>(nameof(AlertLevelChangedReplayEvent))
            .HasValue<CargoObjectSoldReplayEvent>(nameof(CargoObjectSoldReplayEvent))
            .HasValue<CargoProductsOrderedReplayEvent>(nameof(CargoProductsOrderedReplayEvent))
            .HasValue<ChatAnnouncementReplayEvent>(nameof(ChatAnnouncementReplayEvent))
            .HasValue<ChatMessageReplayEvent>(nameof(ChatMessageReplayEvent))
            .HasValue<GenericObjectEvent>(nameof(GenericObjectEvent))
            .HasValue<GenericPlayerEvent>(nameof(GenericPlayerEvent))
            .HasValue<MobStateChangedNPCReplayEvent>(nameof(MobStateChangedNPCReplayEvent))
            .HasValue<MobStateChangedPlayerReplayEvent>(nameof(MobStateChangedPlayerReplayEvent))
            .HasValue<NewsArticlePublishedReplayEvent>(nameof(NewsArticlePublishedReplayEvent))
            .HasValue<ReplayExplosionEvent>(nameof(ReplayExplosionEvent))
            .HasValue<ShuttleReplayEvent>(nameof(ShuttleReplayEvent))
            .HasValue<StoreBuyReplayEvent>(nameof(StoreBuyReplayEvent))
            .HasValue<TechnologyUnlockedReplayEvent>(nameof(TechnologyUnlockedReplayEvent));

        builder.Property(e => e.Position).IsRequired();
        builder.Property(e => e.Severity).IsRequired();
        builder.Property(e => e.Time).IsRequired();
        builder.Property(e => e.EventType).IsRequired();

        builder.HasIndex(e => e.ReplayId);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.Time);
        builder.HasIndex(e => e.Severity);

        builder.HasOne(e => e.Replay)
            .WithMany(r => r.Events)
            .HasForeignKey(e => e.ReplayId);

        builder.Property(e => e.Position)
            .HasConversion(
                v => new NpgsqlTypes.NpgsqlPoint(v.X, v.Y),
                v => new Vector2((float)v.X, (float)v.Y));
    }
}