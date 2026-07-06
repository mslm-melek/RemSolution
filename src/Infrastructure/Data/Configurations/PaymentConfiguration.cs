using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasAgencyTenant(nameof(Payment.PayementDate));

        builder.Property(e => e.PayementAmount)
                   .HasColumnType("decimal(18,2)");

        builder.HasOne(c => c.Client)
               .WithMany(mc => mc.Payments) 
               .HasForeignKey(c => c.ClientId)
               .OnDelete(DeleteBehavior.Cascade);

    }
}
