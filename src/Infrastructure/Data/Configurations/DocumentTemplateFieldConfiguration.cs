using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class DocumentTemplateFieldConfiguration : IEntityTypeConfiguration<DocumentTemplateField>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateField> builder)
    {
        builder.HasAgencyTenant(nameof(DocumentTemplateField.DocumentTemplateId));

        builder.Property(f => f.Placeholder).IsRequired().HasMaxLength(120);
        builder.Property(f => f.DataPath).HasMaxLength(120);
        builder.Property(f => f.FixedValue).HasMaxLength(1000);
        builder.Property(f => f.Label).HasMaxLength(200);

        // One binding per placeholder name per template — the invariant the
        // editor and the generation prompt both assume.
        builder.HasIndex(f => new { f.DocumentTemplateId, f.Placeholder }).IsUnique();
    }
}
