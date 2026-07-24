using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.HasAgencyTenant(nameof(Car.ModelId));

        builder.Property(c => c.Matricule)
                    .IsRequired()
                    .HasMaxLength(50);

        // Plate is unique within an agency, but only among live cars: the
        // filter frees an archived car's matricule for reuse (P.11/P.12).
        builder.HasIndex(c => new { c.AgencyId, c.Matricule })
               .IsUnique()
               .HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.Model)
               .WithMany(mc => mc.Cars)
               .HasForeignKey(c => c.ModelId)
               .OnDelete(DeleteBehavior.SetNull);

        // Home branch. SetNull (as with Model): deleting a branch declassifies
        // its cars rather than deleting them. IX(AgencyId, BranchId) anchors
        // branch-scoped availability/search.
        builder.HasOne(c => c.Branch)
               .WithMany()
               .HasForeignKey(c => c.BranchId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.AgencyId, c.BranchId });

        // New cars are Active; Maintenance/Inactive are set explicitly.
        builder.Property(c => c.Status)
               .HasDefaultValue(CarStatus.Active);

        builder.OwnsMoney(c => c.DailyRate, "DailyRate", "DailyRateCurrency");

        builder.HasMany(c => c.Expenses)
                 .WithOne(e => e.Car)
                 .HasForeignKey(e => e.CarId)
                 .OnDelete(DeleteBehavior.Cascade);

        // Car photo file. Restrict: the delete handler removes the StoredFile
        // row explicitly after clearing the car.
        builder.HasOne(c => c.PhotoFile)
               .WithMany()
               .HasForeignKey(c => c.PhotoFileId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
