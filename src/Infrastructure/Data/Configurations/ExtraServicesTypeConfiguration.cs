using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExtraServicesTypeConfiguration : IEntityTypeConfiguration<ExtraServicesType>
{
    public void Configure(EntityTypeBuilder<ExtraServicesType> builder)
    {
        builder.Property(e => e.Amount)
             .HasColumnType("decimal(18,2)");

        builder.Property(e => e.Name)
               .HasMaxLength(200)
               .UseCollation(DatabaseCollations.AccentInsensitive);

        // Deactivation flag; existing types backfill to active.
        builder.Property(e => e.IsActive)
               .HasDefaultValue(true);
    }
}
