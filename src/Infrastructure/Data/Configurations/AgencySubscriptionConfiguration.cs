using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencySubscriptionConfiguration : IEntityTypeConfiguration<AgencySubscription>
{
    public void Configure(EntityTypeBuilder<AgencySubscription> builder)
    {
        // Cascade: subscriptions are platform billing records that die with the
        // agency (agency deletion is already blocked while tenant data exists).
        builder.HasOne(s => s.Agency)
               .WithMany()
               .HasForeignKey(s => s.AgencyId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a plan referenced by any subscription cannot be deleted.
        builder.HasOne(s => s.Plan)
               .WithMany(p => p.Subscriptions)
               .HasForeignKey(s => s.PlanId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.AgencyId, s.Status });

        // At most one Active subscription per agency, enforced by the database
        // so concurrent admin assignments cannot create an ambiguous state.
        builder.HasIndex(s => s.AgencyId)
               .IsUnique()
               .HasFilter($"[Status] = {(int)SubscriptionStatus.Active}")
               .HasDatabaseName("IX_AgencySubscriptions_OneActivePerAgency");
    }
}
