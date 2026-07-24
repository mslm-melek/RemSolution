using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasAgencyTenant(nameof(Reservation.StartDate));

        builder.OwnsMoney(e => e.Price, "Price", "PriceCurrency");
        builder.OwnsMoney(e => e.PayedPrice, "PayedPrice", "PayedPriceCurrency");

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
