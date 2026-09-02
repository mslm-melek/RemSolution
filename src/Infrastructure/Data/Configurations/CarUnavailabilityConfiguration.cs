using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class CarUnavailabilityConfiguration : IEntityTypeConfiguration<CarUnavailability>
{
    public void Configure(EntityTypeBuilder<CarUnavailability> builder)
    {
        builder.HasAgencyTenant(nameof(CarUnavailability.CarId));

        // Read by the same overlap predicate as hires and holds, so it gets the
        // same shape of index — see IX_Rentings_CarId_Dates.
        builder.HasIndex(e => new { e.CarId, e.StartDate, e.EndDate })
               .HasDatabaseName("IX_CarUnavailabilities_CarId_Dates")
               .IncludeProperties(e => new { e.Reason, e.AgencyId });

        builder.Property(e => e.Note).HasMaxLength(CarUnavailability.MaxNoteLength);

        // Archiving a car takes its blocks with it: they describe that car's
        // calendar and mean nothing without it. Cascade is safe here precisely
        // because Car is soft-deleted — the rows survive the archive and only go
        // when a car is physically removed.
        builder.HasOne(e => e.Car)
               .WithMany(c => c.Unavailabilities)
               .HasForeignKey(e => e.CarId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
