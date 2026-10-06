using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExtraServicesTypeConfiguration : IEntityTypeConfiguration<ExtraServicesType>
{
    public void Configure(EntityTypeBuilder<ExtraServicesType> builder)
    {
        // Cascade for the same reason as ExpenseType: configuration, not data.
        builder.HasOne(e => e.Agency)
               .WithMany()
               .HasForeignKey(e => e.AgencyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.AgencyId, e.TemplateId })
               .IsUnique()
               .HasFilter("[TemplateId] IS NOT NULL");

        builder.HasOne(e => e.Template)
               .WithMany()
               .HasForeignKey(e => e.TemplateId)
               .OnDelete(DeleteBehavior.Restrict);

        // Keeps the old "Amount" column; the currency is new.
        builder.OwnsMoney(e => e.Amount, "Amount", "AmountCurrency");

        builder.Property(e => e.Name)
               .HasMaxLength(200)
               .UseCollation(DatabaseCollations.AccentInsensitive);

        // Deactivation flag; existing types backfill to active.
        builder.Property(e => e.IsActive)
               .HasDefaultValue(true);
    }
}
