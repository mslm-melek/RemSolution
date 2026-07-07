using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class CrossTenantAccessLogConfiguration : IEntityTypeConfiguration<CrossTenantAccessLog>
{
    public void Configure(EntityTypeBuilder<CrossTenantAccessLog> builder)
    {
        builder.Property(l => l.UserId)
               .IsRequired()
               .HasMaxLength(450);

        builder.Property(l => l.Justification)
               .IsRequired()
               .HasMaxLength(512);

        builder.HasIndex(l => l.OccurredOn);
    }
}
