using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.Property(a => a.Name)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(a => a.Email)
               .HasMaxLength(320);

        builder.Property(a => a.PhoneNumber)
               .HasMaxLength(50);

        builder.Property(a => a.Address)
               .HasMaxLength(500);

        builder.Property(a => a.Location)
               .HasColumnType("geography");

        // Countries are seeded reference data; never cascade a country delete into agencies.
        builder.HasOne(a => a.Country)
               .WithMany()
               .HasForeignKey(a => a.CountryId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
