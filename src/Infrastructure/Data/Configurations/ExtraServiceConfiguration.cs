using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExtraServiceConfiguration : IEntityTypeConfiguration<ExtraService>
{
    public void Configure(EntityTypeBuilder<ExtraService> builder)
    {
        builder.Property(e => e.TotalAmount)
             .HasColumnType("decimal(18,2)");

        builder.HasOne(c => c.Renting)
               .WithMany(mc => mc.ExtraServices) 
               .HasForeignKey(c => c.RentingId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.ExtraServicesType)
               .WithMany(mc => mc.ExtraServices) 
               .HasForeignKey(c => c.ExtraServicesTypeId)
               .OnDelete(DeleteBehavior.Cascade);

    }
}
