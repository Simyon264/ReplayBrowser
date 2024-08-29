using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class GenericPlayerEvent : ReplayDbEvent, IEntityTypeConfiguration<GenericPlayerEvent>
{
    /// <summary>
    /// The player info associated with this event. This who this event is about.
    /// </summary>
    public ReplayEventPlayer Target { get; set; }

    /// <summary>
    /// The source of the event. Who was the cause for the Target being affected? Can be null.
    /// </summary>
    public ReplayEventPlayer? Origin { get; set; }

    public void Configure(EntityTypeBuilder<GenericPlayerEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Target).IsRequired();
        builder.Property(e => e.Origin);

        builder.Property(e => e.Target)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));

        builder.Property(e => e.Origin)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}