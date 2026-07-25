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

        builder.Property(e => e.Notes).HasMaxLength(1000);

        // Financial record: never cascade-deleted with a client. Restrict makes
        // a physical client delete fail (clients are archived, not deleted).
        builder.HasOne(c => c.Client)
               .WithMany(mc => mc.Payments)
               .HasForeignKey(c => c.ClientId)
               .OnDelete(DeleteBehavior.Restrict);

        // The renting this payment settles. Restrict: rentings are never removed.
        builder.HasOne(c => c.Renting)
               .WithMany(r => r.Payments)
               .HasForeignKey(c => c.RentingId)
               .OnDelete(DeleteBehavior.Restrict);

        // Self-reference: a reversal entry points back at the payment it offsets.
        builder.HasOne(c => c.ReversesPayment)
               .WithMany()
               .HasForeignKey(c => c.ReversesPaymentId)
               .OnDelete(DeleteBehavior.Restrict);

    }
}
