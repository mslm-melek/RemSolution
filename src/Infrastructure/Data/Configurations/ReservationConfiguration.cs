using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {

        builder.Property(e => e.Price)
                   .HasColumnType("decimal(18,2)");

         builder.Property(e => e.PayedPrice)
                   .HasColumnType("decimal(18,2)");

        builder.HasOne(c => c.Client)
               .WithMany(mc => mc.Reservations) 
               .HasForeignKey(c => c.ClientId)
               .OnDelete(DeleteBehavior.SetNull);

         builder.HasOne(c => c.Renting)
               .WithMany(mc => mc.Reservations) 
               .HasForeignKey(c => c.RentingId)
               .OnDelete(DeleteBehavior.SetNull);

    }
}
