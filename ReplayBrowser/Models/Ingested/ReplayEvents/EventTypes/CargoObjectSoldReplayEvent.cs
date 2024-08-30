using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class CargoObjectSoldReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<CargoObjectSoldReplayEvent>
{
    /// <summary>
    /// The amount of money the objects were sold for
    /// </summary>
    public double Amount;

    public int ObjectsSold;
    public void Configure(EntityTypeBuilder<CargoObjectSoldReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.ObjectsSold).IsRequired();
    }
}