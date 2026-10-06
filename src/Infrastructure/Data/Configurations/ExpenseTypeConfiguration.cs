using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExpenseTypeConfiguration : IEntityTypeConfiguration<ExpenseType>
{
    public void Configure(EntityTypeBuilder<ExpenseType> builder)
    {
        // Cascade, unlike tenant data: the catalog is the agency's configuration,
        // and DeleteAgencyCommand has already refused an agency with expenses.
        builder.HasOne(et => et.Agency)
               .WithMany()
               .HasForeignKey(et => et.AgencyId)
               .OnDelete(DeleteBehavior.Cascade);

        // One copy of a template per agency — what makes copying idempotent.
        builder.HasIndex(et => new { et.AgencyId, et.TemplateId })
               .IsUnique()
               .HasFilter("[TemplateId] IS NOT NULL");

        builder.HasOne(et => et.Template)
               .WithMany()
               .HasForeignKey(et => et.TemplateId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(et => et.WithNotif)
                   .IsRequired();

        builder.Property(et => et.Name)
               .HasMaxLength(200)
               .UseCollation(DatabaseCollations.AccentInsensitive);

        // Deactivation flag; existing types backfill to active.
        builder.Property(et => et.IsActive)
               .HasDefaultValue(true);

        // Relation inverse : ExpenseType → Expenses
        builder.HasMany(et => et.Expenses)
               .WithOne(e => e.ExpenseType)
               .HasForeignKey(e => e.ExpenseTypeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
