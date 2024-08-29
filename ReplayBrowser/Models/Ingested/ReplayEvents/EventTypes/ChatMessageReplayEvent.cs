using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ChatMessageReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<ChatMessageReplayEvent>
{
    public string Message;

    public ReplayEventPlayer Sender;

    public string Type;
    public void Configure(EntityTypeBuilder<ChatMessageReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Message).IsRequired();
        builder.Property(e => e.Sender).IsRequired();
        builder.Property(e => e.Type).IsRequired();

        builder.Property(e => e.Sender)
            .HasConversion(
                v => JsonConvert.SerializeObject(v),
                v => JsonConvert.DeserializeObject<ReplayEventPlayer>(v));
    }
}
