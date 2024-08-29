using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class CargoProductsOrderedReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<CargoProductsOrderedReplayEvent>
{
    public CargoReplayProduct[] Products;

    public ReplayEventPlayer ApprovedBy;
    public void Configure(EntityTypeBuilder<CargoProductsOrderedReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Products).IsRequired();
        builder.Property(e => e.ApprovedBy).IsRequired();

        builder.Property(e => e.ApprovedBy)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));

        builder.Property(e => e.Products)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<CargoReplayProduct[]>(v));
    }
}

public class CargoReplayProduct
{
    public string ProductId;

    public string Reason = "";

    public ReplayEventPlayer OrderedBy;
}