using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class GenericObjectEvent : ReplayDbEvent, IEntityTypeConfiguration<GenericObjectEvent>
{
    public string Target;

    public string? Origin;
    public void Configure(EntityTypeBuilder<GenericObjectEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Target).IsRequired();
        builder.Property(e => e.Origin);
    }
}
