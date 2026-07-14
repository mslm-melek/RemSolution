using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        // IX(AgencyId, Name): branch lists are always tenant-scoped and
        // ordered/filtered by name.
        builder.HasAgencyTenant(nameof(Branch.Name));

        builder.Property(b => b.Name)
               .IsRequired()
               .HasMaxLength(200);

        // The spatial index on Location cannot be expressed through the EF
        // model (SQL Server spatial indexes aren't supported by the
        // provider); it is created by raw SQL in the AddBranch migration.
        builder.Property(b => b.Location)
               .HasColumnType("geography");

        // Countries are seeded reference data; never cascade a country delete into branches.
        builder.HasOne(b => b.Country)
               .WithMany()
               .HasForeignKey(b => b.CountryId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
