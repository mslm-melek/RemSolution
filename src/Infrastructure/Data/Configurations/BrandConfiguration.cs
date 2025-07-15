using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.HasMany(b => b.ModelCars)
                   .WithOne(mc => mc.Brand) // navigation inverse dans ModelCar
                   .HasForeignKey(mc => mc.BrandId)
                   .OnDelete(DeleteBehavior.Cascade);
    }
}
