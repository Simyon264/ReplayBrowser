using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ReplayExplosionEvent : ReplayDbEvent, IEntityTypeConfiguration<ReplayExplosionEvent>
{
    public ReplayEventPlayer? Source;

    public float Intensity;

    public float Slope;

    public float MaxTileIntensity;

    public float TileBreakScale;

    public int MaxTileBreak;

    public bool CanCreateVacuum;

    public string Type;

    public void Configure(EntityTypeBuilder<ReplayExplosionEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Source);
        builder.Property(e => e.Intensity).IsRequired();
        builder.Property(e => e.Slope).IsRequired();
        builder.Property(e => e.MaxTileIntensity).IsRequired();
        builder.Property(e => e.TileBreakScale).IsRequired();
        builder.Property(e => e.MaxTileBreak).IsRequired();
        builder.Property(e => e.CanCreateVacuum).IsRequired();
        builder.Property(e => e.Type).IsRequired();

        builder.Property(e => e.Source)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}