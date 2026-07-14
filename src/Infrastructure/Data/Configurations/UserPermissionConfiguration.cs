using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        // The FK to AspNetUsers is configured in ApplicationDbContext, where
        // the Identity user type is visible.
        builder.Property(p => p.UserId)
               .IsRequired()
               .HasMaxLength(450);

        builder.Property(p => p.Permission)
               .IsRequired()
               .HasMaxLength(100);

        // One grant per (user, permission); the index also serves the
        // claims-factory lookup at sign-in.
        builder.HasIndex(p => new { p.UserId, p.Permission })
               .IsUnique();
    }
}
