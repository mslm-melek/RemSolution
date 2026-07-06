using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RemSolution.Infrastructure.Data.Configurations;

public static class TenantConfigurationExtensions
{
    /// <summary>
    /// Tenant key rule for agency-scoped entities: the Agency FK is Restrict
    /// (never cascade an agency delete through tenant data) and the tenant-scoped
    /// index leads with AgencyId, optionally extended with extra columns
    /// (e.g. <c>HasAgencyTenant(nameof(Car.ModelId))</c> → IX(AgencyId, ModelId)).
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasAgencyTenant<TEntity>(this EntityTypeBuilder<TEntity> builder, params string[] indexColumns)
        where TEntity : class
    {
        builder.HasOne(typeof(Agency), nameof(Car.Agency))
               .WithMany()
               .HasForeignKey(nameof(Car.AgencyId))
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(new[] { nameof(Car.AgencyId) }.Concat(indexColumns).ToArray());

        return builder;
    }
}
