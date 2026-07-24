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
        // Feature toggles are agency-owned config (like AgencySettings), so they
        // cascade away with the agency; DeleteAgencyCommand still guards against
        // deleting an agency that owns business data.
        builder.HasOne(f => f.Agency)
               .WithMany()
               .HasForeignKey(f => f.AgencyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.AgencyId, f.Feature })
               .IsUnique();

        builder.Property(f => f.Feature)
               .IsRequired()
               .HasMaxLength(100);
    }
}
