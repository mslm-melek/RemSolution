using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class CarExpenseScheduleConfiguration : IEntityTypeConfiguration<CarExpenseSchedule>
{
    public void Configure(EntityTypeBuilder<CarExpenseSchedule> builder)
    {
        // By hand rather than HasAgencyTenant: the unique index below is already an
        // AgencyId-leading index, so the helper's would be a duplicate prefix.
        builder.HasOne(s => s.Agency)
               .WithMany()
               .HasForeignKey(s => s.AgencyId)
               .OnDelete(DeleteBehavior.Restrict);

        // A car states its rule for a type once; also the index the reads by car use.
        builder.HasIndex(s => new { s.AgencyId, s.CarId, s.ExpenseTypeId })
               .IsUnique();

        builder.HasOne(s => s.Car)
               .WithMany(c => c.ExpenseSchedules)
               .HasForeignKey(s => s.CarId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict, like Expense: types are deactivated, never deleted.
        builder.HasOne(s => s.ExpenseType)
               .WithMany()
               .HasForeignKey(s => s.ExpenseTypeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
