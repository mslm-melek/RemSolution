using RemSolution.Domain.Entities;
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

        builder.HasOne(c => c.Model)
               .WithMany(mc => mc.Cars) 
               .HasForeignKey(c => c.ModelId)
               .OnDelete(DeleteBehavior.SetNull);

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
