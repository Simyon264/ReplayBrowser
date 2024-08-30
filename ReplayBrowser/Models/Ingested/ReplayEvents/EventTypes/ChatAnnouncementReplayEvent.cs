using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Data.Models;

namespace ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes;

public class ChatAnnouncementReplayEvent : ReplayDbEvent, IEntityTypeConfiguration<ChatAnnouncementReplayEvent>
{
    public string Message;

    public string Sender;
    public void Configure(EntityTypeBuilder<ChatAnnouncementReplayEvent> builder)
    {
        builder.HasBaseType<ReplayDbEvent>();
        builder.Property(e => e.Message).IsRequired();
        builder.Property(e => e.Sender).IsRequired();
    }
}
