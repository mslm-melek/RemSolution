using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class RentingConfiguration : IEntityTypeConfiguration<Renting>
{
    public void Configure(EntityTypeBuilder<Renting> builder)
    {
        builder.HasAgencyTenant(nameof(Renting.RentingState));

        // The overlap predicate AvailabilityChecker runs for every candidate car
        // (CarId = @car AND StartDate < @end AND EndDate > @start). IX(AgencyId,
        // RentingState) cannot serve it — it leads with the wrong column and
        // carries no dates — so the alternative is a scan of every hire the
        // agency ever recorded, growing with its history rather than its fleet.
        // The included columns keep it a covering seek: marketplace search runs
        // this once per nearby car.
        builder.HasIndex(e => new { e.CarId, e.StartDate, e.EndDate })
               .HasDatabaseName("IX_Rentings_CarId_Dates")
               .IncludeProperties(e => new { e.RentingState, e.AgencyId });

        builder.OwnsMoney(e => e.Price, "Price", "PriceCurrency");
        builder.OwnsMoney(e => e.DepositAmount, "DepositAmount", "DepositAmountCurrency");
        builder.OwnsMoney(e => e.CancellationFee, "CancellationFee", "CancellationFeeCurrency");
        builder.OwnsMoney(e => e.DepositRetainedAmount, "DepositRetainedAmount", "DepositRetainedAmountCurrency");

        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.HasOne(c => c.Car)
               .WithMany(mc => mc.Rentings) 
               .HasForeignKey(c => c.CarId)
               .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(c => c.Client)
               .WithMany(mc => mc.Rentings) 
               .HasForeignKey(c => c.ClientId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.SecondClient)
               .WithMany(mc => mc.SecondRentings) 
               .HasForeignKey(c => c.SecondClientId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(c => c.ExtraServices)
                 .WithOne(e => e.Renting)
                 .HasForeignKey(e => e.RentingId)
                 .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.RentingHistories)
                 .WithOne(e => e.Renting)
                 .HasForeignKey(e => e.RentingId)
                 .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Reservations)
                 .WithOne(e => e.Renting)
                 .HasForeignKey(e => e.RentingId)
                 .OnDelete(DeleteBehavior.Cascade);
    }
}
