using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");

        // Not HasAgencyTenant(): a rate belongs to the marketplace, not to an
        // agency, and an anonymous visitor reads it (see ExchangeRate).
        builder.Property(r => r.FromCurrency).IsRequired().HasMaxLength(3).IsFixedLength();
        builder.Property(r => r.ToCurrency).IsRequired().HasMaxLength(3).IsFixedLength();

        // One rate per ordered pair — the reverse is derived, never stored, so
        // there is nothing that can drift out of step with this row.
        builder.HasIndex(r => new { r.FromCurrency, r.ToCurrency }).IsUnique();

        // A rate is a ratio, not money: it needs places, and the amounts it is
        // applied to are already rounded to two. Six is enough for a weak
        // currency against a strong one (1 TND = 0.294118 EUR).
        builder.Property(r => r.Rate).HasPrecision(18, 6);

        builder.ToTable(t => t.HasCheckConstraint("CK_ExchangeRates_Rate", "[Rate] > 0"));
    }
}
