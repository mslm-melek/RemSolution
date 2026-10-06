using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class ExpenseTypeTemplateConfiguration : IEntityTypeConfiguration<ExpenseTypeTemplate>
{
    public void Configure(EntityTypeBuilder<ExpenseTypeTemplate> builder)
    {
        builder.Property(t => t.Name)
               .IsRequired()
               .HasMaxLength(200)
               .UseCollation(DatabaseCollations.AccentInsensitive);

        builder.Property(t => t.IsActive)
               .HasDefaultValue(true);
    }
}

public class ExtraServicesTypeTemplateConfiguration : IEntityTypeConfiguration<ExtraServicesTypeTemplate>
{
    public void Configure(EntityTypeBuilder<ExtraServicesTypeTemplate> builder)
    {
        builder.Property(t => t.Name)
               .IsRequired()
               .HasMaxLength(200)
               .UseCollation(DatabaseCollations.AccentInsensitive);

        builder.Property(t => t.IsActive)
               .HasDefaultValue(true);
    }
}
