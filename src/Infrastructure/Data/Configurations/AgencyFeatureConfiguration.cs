using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencyFeatureConfiguration : IEntityTypeConfiguration<AgencyFeature>
{
    public void Configure(EntityTypeBuilder<AgencyFeature> builder)
    {
        // Not HasAgencyTenant(): the tenant-scoped index must be unique — one
        // toggle row per (agency, feature) — which the extension doesn't do.
        // The FK rule is the same (never cascade an agency delete).
        builder.HasOne(f => f.Agency)
               .WithMany()
               .HasForeignKey(f => f.AgencyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.AgencyId, f.Feature })
               .IsUnique();

        builder.Property(f => f.Feature)
               .IsRequired()
               .HasMaxLength(100);
    }
}
