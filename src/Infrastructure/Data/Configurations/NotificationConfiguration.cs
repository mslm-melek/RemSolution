using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        // Tenant index leads with AgencyId; the recipient next, because every
        // read of this table is one person's inbox, newest first.
        builder.HasAgencyTenant(nameof(Notification.RecipientUserId), nameof(Notification.CreatedAt));

        // 450 is the Identity key length, as on ChatMessage.SenderUserId.
        builder.Property(n => n.RecipientUserId).HasMaxLength(450);
        builder.Property(n => n.SentByUserId).HasMaxLength(450);
        builder.Property(n => n.RecipientEmail).HasMaxLength(256);
        builder.Property(n => n.MessageKey).IsRequired().HasMaxLength(100);
        builder.Property(n => n.Link).HasMaxLength(400);
        builder.Property(n => n.ArgsJson).HasMaxLength(2000);
        builder.Property(n => n.DedupKey).IsRequired().HasMaxLength(200);

        // What makes the sweep idempotent: the same alert for the same person
        // about the same thing in the same time bucket cannot be inserted twice,
        // whatever races or retries happen. The sweep still checks first — this
        // index is the guarantee, not the mechanism.
        builder.HasIndex(n => new { n.AgencyId, n.DedupKey }).IsUnique();

        // Serves the unread badge, which is polled far more often than the list
        // is opened. Filtered so it only carries the rows the count is about.
        builder.HasIndex(n => new { n.AgencyId, n.RecipientUserId, n.ReadAt })
               .HasFilter("[ReadAt] IS NULL");

        // Restrict, like every other client reference: a client is archived, not
        // removed, and the record of having written to them outlives the booking
        // it was about.
        builder.HasOne(n => n.Client)
               .WithMany()
               .HasForeignKey(n => n.ClientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
