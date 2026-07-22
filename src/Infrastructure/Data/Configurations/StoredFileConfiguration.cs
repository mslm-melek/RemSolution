using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        // Tenant-scoped, and the extra index column makes the per-agency dedup
        // lookup (WHERE AgencyId = @t AND Sha256 = @h) an index seek.
        builder.HasAgencyTenant(nameof(StoredFile.Sha256));

        builder.Property(f => f.Path).IsRequired().HasMaxLength(1024);
        builder.Property(f => f.Url).IsRequired().HasMaxLength(1024);
        builder.Property(f => f.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(f => f.MimeType).IsRequired().HasMaxLength(255);

        // Hex SHA-256 is always 64 chars; fixing the width keeps the dedup index
        // tight.
        builder.Property(f => f.Sha256).IsRequired().HasMaxLength(64);
    }
}
