using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        // Tenant index leads with AgencyId; RentingId next, since every read is
        // "the messages of one thread, oldest first".
        builder.HasAgencyTenant(nameof(ChatMessage.RentingId), nameof(ChatMessage.SentAt));

        builder.Property(m => m.Body).IsRequired().HasMaxLength(2000);
        builder.Property(m => m.SenderUserId).HasMaxLength(450);
        builder.Property(m => m.SenderName).HasMaxLength(256);

        // Restrict: a renting is never physically removed, and its conversation
        // must not be silently cascaded away if that ever changes.
        builder.HasOne(m => m.Renting)
               .WithMany(r => r.ChatMessages)
               .HasForeignKey(m => m.RentingId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
