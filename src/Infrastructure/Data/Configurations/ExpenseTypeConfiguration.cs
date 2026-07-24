using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExpenseTypeConfiguration : IEntityTypeConfiguration<ExpenseType>
{
    public void Configure(EntityTypeBuilder<ExpenseType> builder)
    {
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
