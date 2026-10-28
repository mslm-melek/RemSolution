using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencyReportMessageConfiguration : IEntityTypeConfiguration<AgencyReportMessage>
{
    public void Configure(EntityTypeBuilder<AgencyReportMessage> builder)
    {
        builder.ToTable("AgencyReportMessages");

        // Not HasAgencyTenant(), for the report's own reason: neither party to
        // the conversation carries a tenant claim (see AgencyReport).

        builder.Property(m => m.Body)
               .IsRequired()
               .HasMaxLength(AgencyReport.MaxMessageLength);

        builder.Property(m => m.AuthorUserId).HasMaxLength(450);

        // The thread is read in order, one report at a time.
        builder.HasIndex(m => new { m.AgencyReportId, m.SentAt });

        // The aggregate hands out a read-only list, so EF writes the backing field
        // rather than the property (see AgencyReport.Messages).
        builder.HasOne(m => m.AgencyReport)
               .WithMany(r => r.Messages)
               .HasForeignKey(m => m.AgencyReportId)
               .OnDelete(DeleteBehavior.Cascade)
               .Metadata.PrincipalToDependent!
               .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
