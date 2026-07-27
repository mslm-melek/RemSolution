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
        builder.OwnsMoney(f => f.TotalAmount, "TotalAmount", "TotalAmountCurrency");

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
