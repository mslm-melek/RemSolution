using RemSolution.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // The FK to AspNetUsers is configured in ApplicationDbContext, where
        // the Identity user type is visible.
        builder.Property(t => t.UserId)
               .IsRequired()
               .HasMaxLength(450);

        // SHA-256 as Base64 is 44 chars; give headroom.
        builder.Property(t => t.TokenHash)
               .IsRequired()
               .HasMaxLength(88);

        builder.Property(t => t.ReplacedByTokenHash)
               .HasMaxLength(88);

        builder.Property(t => t.SecurityStamp)
               .IsRequired()
               .HasMaxLength(256);

        // Lookups are always by token hash; it is unique per issued token.
        builder.HasIndex(t => t.TokenHash)
               .IsUnique();

        // Revocation ("kill every session for this user") scans by user.
        builder.HasIndex(t => t.UserId);
    }
}
