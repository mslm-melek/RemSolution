using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasAgencyTenant(nameof(Expense.ExpenseDate));

        builder.Property(e => e.ExpenseAmount)
                   .HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.Car)
              .WithMany(c => c.Expenses) // si navigation dans Car
              .HasForeignKey(e => e.CarId)
              .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ExpenseType)
               .WithMany(et => et.Expenses) // si navigation dans ExpenseType
               .HasForeignKey(e => e.ExpenseTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        // Facture file FK (dormant until the Expense upload flow is built).
        builder.HasOne(e => e.FactureFile)
               .WithMany()
               .HasForeignKey(e => e.FactureFileId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
