using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class RentingFeeConfiguration : IEntityTypeConfiguration<RentingFee>
{
    public void Configure(EntityTypeBuilder<RentingFee> builder)
    {
        builder.HasAgencyTenant();

        builder.OwnsMoney(f => f.Amount, "Amount", "AmountCurrency");

        builder.Property(f => f.Note).HasMaxLength(500);

        // The aggregate hands out a read-only list, so EF writes the backing field
        // rather than the property (see Renting.Fees).
        builder.HasOne(f => f.Renting)
               .WithMany(r => r.Fees)
               .HasForeignKey(f => f.RentingId)
               .OnDelete(DeleteBehavior.Cascade)
               .Metadata.PrincipalToDependent!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
