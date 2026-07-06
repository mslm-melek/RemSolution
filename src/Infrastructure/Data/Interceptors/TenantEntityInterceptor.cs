using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RemSolution.Infrastructure.Data.Interceptors;

/// <summary>
/// Write-side tenant enforcement (query filters only guard reads). When a
/// tenant is present:
/// <list type="bullet">
/// <item>Inserts are stamped with the tenant's AgencyId, overwriting whatever
/// the handler set — a request can never create rows in another agency.</item>
/// <item>AgencyId is never written on update (the column is excluded from the
/// UPDATE), so a row can never move to another agency.</item>
/// <item>Updating or deleting a row whose AgencyId is not the current tenant's
/// throws ForbiddenAccessException (403).</item>
/// </list>
/// Without a tenant (seeding, platform admin) writes pass through untouched.
/// </summary>
public class TenantEntityInterceptor : SaveChangesInterceptor
{
    private readonly ITenantProvider _tenant;

    public TenantEntityInterceptor(ITenantProvider tenant)
    {
        _tenant = tenant;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnforceTenant(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        EnforceTenant(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceTenant(DbContext? context)
    {
        if (context == null || _tenant.AgencyId is not int agencyId) return;

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.AgencyId = agencyId;
                    break;

                case EntityState.Modified:
                    var agencyIdProperty = entry.Property(e => e.AgencyId);

                    if (agencyIdProperty.IsModified)
                    {
                        // Tracked entities revert to the loaded value; detached
                        // updates just never write the column, so the stored
                        // value survives either way.
                        agencyIdProperty.CurrentValue = agencyIdProperty.OriginalValue;
                        agencyIdProperty.IsModified = false;
                    }

                    if (entry.Entity.AgencyId != agencyId)
                    {
                        throw new ForbiddenAccessException();
                    }

                    break;

                case EntityState.Deleted:
                    if (entry.Entity.AgencyId != agencyId)
                    {
                        throw new ForbiddenAccessException();
                    }

                    break;
            }
        }
    }
}
