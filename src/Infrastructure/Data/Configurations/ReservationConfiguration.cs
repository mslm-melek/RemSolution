using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        // IX(AgencyId, Status, ExpiresAt): serves status-filtered lists and the
        // per-agency expiry sweep (Pending holds past ExpiresAt).
        builder.HasAgencyTenant(nameof(Reservation.Status), nameof(Reservation.ExpiresAt));

        builder.OwnsMoney(e => e.Price, "Price", "PriceCurrency");
        builder.OwnsMoney(e => e.PayedPrice, "PayedPrice", "PayedPriceCurrency");
        builder.OwnsMoney(e => e.DepositAmount, "DepositAmount", "DepositAmountCurrency");

        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.RejectedReason).HasMaxLength(1000);
        builder.Property(e => e.CancelledReason).HasMaxLength(1000);
        builder.Property(e => e.ExpiredReason).HasMaxLength(1000);

        // The held car — cleared (not cascaded) if the car row is ever removed.
        builder.HasOne(c => c.Car)
               .WithMany()
               .HasForeignKey(c => c.CarId)
               .OnDelete(DeleteBehavior.SetNull);

        // Financial record: never deleted or orphaned by a client delete.
        // Restrict makes a physical client delete fail (clients are archived).
        builder.HasOne(c => c.Client)
               .WithMany(mc => mc.Reservations)
               .HasForeignKey(c => c.ClientId)
               .OnDelete(DeleteBehavior.Restrict);

         builder.HasOne(c => c.Renting)
               .WithMany(mc => mc.Reservations) 
               .HasForeignKey(c => c.RentingId)
               .OnDelete(DeleteBehavior.SetNull);

    }
}
