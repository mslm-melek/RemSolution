using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class CarImageConfiguration : IEntityTypeConfiguration<CarImage>
{
    public void Configure(EntityTypeBuilder<CarImage> builder)
    {
        // IX(AgencyId, CarId): a car's gallery is always read tenant-scoped by car.
        builder.HasAgencyTenant(nameof(CarImage.CarId));

        // Gallery rows die with the car. The StoredFile bytes they point at are
        // cleaned separately (see DeleteCarCommand), like Car.PhotoFile.
        builder.HasOne(ci => ci.Car)
               .WithMany(c => c.Images)
               .HasForeignKey(ci => ci.CarId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict on every StoredFile FK: the delete handler removes the rows
        // explicitly after detaching them, so a file is never deleted out from
        // under a row that still references it.
        builder.HasOne(ci => ci.OriginalFile)
               .WithMany()
               .HasForeignKey(ci => ci.OriginalFileId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ci => ci.ThumbnailFile)
               .WithMany()
               .HasForeignKey(ci => ci.ThumbnailFileId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ci => ci.MediumFile)
               .WithMany()
               .HasForeignKey(ci => ci.MediumFileId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
