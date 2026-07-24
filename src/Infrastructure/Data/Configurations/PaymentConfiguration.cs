using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasAgencyTenant(nameof(Payment.PayementDate));

        builder.OwnsMoney(e => e.PayementAmount, "PayementAmount", "PayementAmountCurrency");

        // Financial record: never cascade-deleted with a client. Restrict makes
        // a physical client delete fail (clients are archived, not deleted).
        builder.HasOne(c => c.Client)
               .WithMany(mc => mc.Payments)
               .HasForeignKey(c => c.ClientId)
               .OnDelete(DeleteBehavior.Restrict);

    }
}
