using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.Property(f => f.Feature)
               .IsRequired()
               .HasMaxLength(100);

        // One row per plan+feature.
        builder.HasIndex(f => new { f.PlanId, f.Feature }).IsUnique();

        // Deleting a plan removes its feature rows (the plan itself is
        // Restrict-guarded while any subscription references it).
        builder.HasOne(f => f.Plan)
               .WithMany(p => p!.Features)
               .HasForeignKey(f => f.PlanId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
