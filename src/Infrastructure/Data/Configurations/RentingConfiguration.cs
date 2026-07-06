using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class RentingConfiguration : IEntityTypeConfiguration<Renting>
{
    public void Configure(EntityTypeBuilder<Renting> builder)
    {
        builder.HasAgencyTenant(nameof(Renting.RentingState));

        builder.Property(e => e.Price)
                   .HasColumnType("decimal(18,2)");

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
