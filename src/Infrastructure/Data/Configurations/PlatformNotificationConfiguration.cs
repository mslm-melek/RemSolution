using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class PlatformNotificationConfiguration : IEntityTypeConfiguration<PlatformNotification>
{
    public void Configure(EntityTypeBuilder<PlatformNotification> builder)
    {
        builder.ToTable("PlatformNotifications");

        // Same lengths as Notifications, so a key or a link that fits one fits both.
        builder.Property(n => n.RecipientUserId).IsRequired().HasMaxLength(450);
        builder.Property(n => n.MessageKey).IsRequired().HasMaxLength(100);
        builder.Property(n => n.Link).HasMaxLength(400);
        builder.Property(n => n.ArgsJson).HasMaxLength(2000);
        builder.Property(n => n.DedupKey).IsRequired().HasMaxLength(200);

        // The guarantee behind the dedup check (see Notifications).
        builder.HasIndex(n => n.DedupKey).IsUnique();

        // One person's inbox, newest first; and the unread badge.
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAt });
        builder.HasIndex(n => new { n.RecipientUserId, n.ReadAt })
               .HasFilter("[ReadAt] IS NULL");
    }
}
