using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {

        // Foreign keys configuration (optional relationships)
        builder.HasOne(c => c.BirthCountry)
               .WithMany()
               .HasForeignKey(c => c.BirthCountryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CINDeliveranceCountry)
               .WithMany()
               .HasForeignKey(c => c.CINDeliveranceCountryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.PasseportDeliveranceCountry)
               .WithMany()
               .HasForeignKey(c => c.PasseportDeliveranceCountryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.DrivingLicenceDeliveranceCountry)
               .WithMany()
               .HasForeignKey(c => c.DrivingLicenceDeliveranceCountryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Rentings)
               .WithOne(r => r.Client)
               .HasForeignKey(r => r.ClientId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.SecondRentings)
               .WithOne(r => r.SecondClient)
               .HasForeignKey(r => r.SecondClientId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(c => c.Reservations)
               .WithOne(res => res.Client)
               .HasForeignKey(res => res.ClientId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Payments)
               .WithOne(p => p.Client)
               .HasForeignKey(p => p.ClientId)
               .OnDelete(DeleteBehavior.Cascade);



    }
}
