using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ModelCarConfiguration : IEntityTypeConfiguration<ModelCar>
{
    public void Configure(EntityTypeBuilder<ModelCar> builder)
    {

        builder.HasOne(c => c.Brand)
               .WithMany(mc => mc.ModelCars) 
               .HasForeignKey(c => c.BrandId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Cars)
                 .WithOne(e => e.Model)
                 .HasForeignKey(e => e.ModelId)
                 .OnDelete(DeleteBehavior.SetNull);
    }
}
