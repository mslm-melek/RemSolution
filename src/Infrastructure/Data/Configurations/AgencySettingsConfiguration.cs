using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencySettingsConfiguration : IEntityTypeConfiguration<AgencySettings>
{
    public void Configure(EntityTypeBuilder<AgencySettings> builder)
    {
        builder.Property(s => s.CurrencyCode)
               .IsRequired()
               .HasMaxLength(3)
               .IsUnicode(false);

        // 1:1 with Agency, keyed on the FK; the settings row is created with the
        // agency and dies with it.
        builder.HasOne(s => s.Agency)
               .WithOne(a => a.Settings)
               .HasForeignKey<AgencySettings>(s => s.AgencyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
