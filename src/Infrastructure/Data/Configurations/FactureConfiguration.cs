using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class FactureConfiguration : IEntityTypeConfiguration<Facture>
{
    public void Configure(EntityTypeBuilder<Facture> builder)
    {
        builder.HasAgencyTenant(nameof(Facture.RentingId));

        builder.Property(f => f.Number).IsRequired().HasMaxLength(40);
        builder.Property(f => f.Language).IsRequired().HasMaxLength(16);

        builder.OwnsMoney(f => f.RentalAmount, "RentalAmount", "RentalAmountCurrency");
        builder.OwnsMoney(f => f.ExtraServicesAmount, "ExtraServicesAmount", "ExtraServicesAmountCurrency");
        builder.OwnsMoney(f => f.FeesAmount, "FeesAmount", "FeesAmountCurrency");
        builder.OwnsMoney(f => f.TotalAmount, "TotalAmount", "TotalAmountCurrency");
        builder.OwnsMoney(f => f.NetAmount, "NetAmount", "NetAmountCurrency");
        builder.OwnsMoney(f => f.VatAmount, "VatAmount", "VatAmountCurrency");
        builder.OwnsMoney(f => f.FiscalStampAmount, "FiscalStampAmount", "FiscalStampAmountCurrency");
        builder.OwnsMoney(f => f.TotalDue, "TotalDue", "TotalDueCurrency");

        // 5,2 covers every real rate (0.00–999.99%) in the smallest column that
        // does; the default decimal mapping would be 18,2.
        builder.Property(f => f.VatRatePercent).HasColumnType("decimal(5,2)");

        builder.Property(f => f.TaxIdentifier).HasMaxLength(40);

        // The frozen courtesy rate must match the precision of the row it was
        // copied from — ExchangeRate.Rate is 18,6. The default 18,2 would round
        // 0.296247 to 0.30 and the freeze would reproduce nothing.
        builder.Property(f => f.DisplayExchangeRate).HasPrecision(18, 6);
        builder.Property(f => f.DisplayCurrency).HasMaxLength(3);

        // See ContractConfiguration: the database owns the numbering invariant.
        builder.HasIndex(f => new { f.AgencyId, f.Year, f.SequenceNumber }).IsUnique();

        builder.HasOne(f => f.Renting)
               .WithMany(r => r.Factures)
               .HasForeignKey(f => f.RentingId)
               .OnDelete(DeleteBehavior.Restrict);

        // Financial record: a client archive must not orphan their invoices.
        builder.HasOne(f => f.Client)
               .WithMany()
               .HasForeignKey(f => f.ClientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.DocumentFile)
               .WithMany()
               .HasForeignKey(f => f.DocumentFileId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.TemplateName).HasMaxLength(200);

        // See ContractConfiguration.
        builder.HasOne(f => f.DocumentTemplate)
               .WithMany()
               .HasForeignKey(f => f.DocumentTemplateId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
