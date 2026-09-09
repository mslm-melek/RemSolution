using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencySettingsConfiguration : IEntityTypeConfiguration<AgencySettings>
{
    public void Configure(EntityTypeBuilder<AgencySettings> builder)
    {
        builder.Property(s => s.CurrencyCode)
               .IsRequired()
               .HasMaxLength(3)
               .IsUnicode(false);

        builder.Property(s => s.TaxIdentifier).HasMaxLength(40);

        // Same shape as CurrencyCode above: an ISO 4217 code, never Unicode.
        builder.Property(s => s.InvoiceDisplayCurrency)
               .HasMaxLength(3)
               .IsUnicode(false);

        // Same shapes as the frozen copies on Facture, so a value cannot round
        // differently on its way from the setting to the invoice.
        builder.Property(s => s.VatRatePercent).HasColumnType("decimal(5,2)");
        builder.Property(s => s.FiscalStampAmount).HasColumnType("decimal(18,2)");

        // 1:1 with Agency, keyed on the FK; the settings row is created with the
        // agency and dies with it.
        builder.HasOne(s => s.Agency)
               .WithOne(a => a.Settings)
               .HasForeignKey<AgencySettings>(s => s.AgencyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
