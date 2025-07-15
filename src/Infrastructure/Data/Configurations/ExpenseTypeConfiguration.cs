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

        // Relation inverse : ExpenseType → Expenses
        builder.HasMany(et => et.Expenses)
               .WithOne(e => e.ExpenseType)
               .HasForeignKey(e => e.ExpenseTypeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
