using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        // IX(AgencyId, Kind, Language): every read is "this agency's contract
        // templates in this language" — the picker, and the default lookup.
        builder.HasAgencyTenant(nameof(DocumentTemplate.Kind), nameof(DocumentTemplate.Language));

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Language).IsRequired().HasMaxLength(16);

        // The block document. nvarchar(max): a contract's clauses are long-form
        // legal text and there is no useful ceiling to impose.
        builder.Property(t => t.BlocksJson).IsRequired();

        builder.HasMany(t => t.Fields)
               .WithOne(f => f.DocumentTemplate)
               .HasForeignKey(f => f.DocumentTemplateId)
               // The bindings are part of the template, not records of their own.
               .OnDelete(DeleteBehavior.Cascade);
    }
}
