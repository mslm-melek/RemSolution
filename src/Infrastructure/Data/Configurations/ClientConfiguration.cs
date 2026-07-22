using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasAgencyTenant();

        builder.Property(c => c.MarketplaceUserId)
               .HasMaxLength(450);

        // Bad-client moderation notes; bounded so the column stays a plain
        // nvarchar rather than nvarchar(max). IsFlagged is a non-nullable bool
        // (defaults to false via the CLR default; existing rows backfill to 0).
        builder.Property(c => c.Notes)
               .HasMaxLength(2000);

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

        // Identity-document files. Restrict (never cascade a file delete through
        // a client): the delete handler removes the StoredFile rows explicitly
        // after clearing the client, and shared physical bytes are reference-
        // counted by path.
        builder.HasOne(c => c.CINFile)
               .WithMany()
               .HasForeignKey(c => c.CINFileId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.DrivingLicenceFile)
               .WithMany()
               .HasForeignKey(c => c.DrivingLicenceFileId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.PasseportFile)
               .WithMany()
               .HasForeignKey(c => c.PasseportFileId)
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
