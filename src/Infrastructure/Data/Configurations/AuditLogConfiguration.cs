using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(l => l.UserId)
               .HasMaxLength(450);

        builder.Property(l => l.UserName)
               .HasMaxLength(256);

        builder.Property(l => l.Action)
               .IsRequired()
               .HasMaxLength(128);

        builder.Property(l => l.Entity)
               .IsRequired()
               .HasMaxLength(128);

        builder.Property(l => l.EntityId)
               .HasMaxLength(256);

        builder.Property(l => l.CorrelationId)
               .HasMaxLength(128);

        // Before/After are JSON documents of arbitrary size — leave them nvarchar(max).

        // The two questions the trail answers: "what happened in this agency"
        // and "who did what, when".
        builder.HasIndex(l => new { l.AgencyId, l.OccurredOn });
        builder.HasIndex(l => l.OccurredOn);
    }
}
