namespace RemSolution.Domain.Constants;

public abstract class Roles
{
    /// <summary>
    /// Operates the platform: manages agencies, plans and subscriptions.
    /// Carries no AgencyId claim — cross-tenant reads only through the
    /// audited ICrossTenantAccess path.
    /// </summary>
    public const string PlatformAdministrator = nameof(PlatformAdministrator);

    /// <summary>Manages one agency (its staff, settings). Tenant-scoped.</summary>
    public const string AgencyAdministrator = nameof(AgencyAdministrator);

    /// <summary>Day-to-day agency work (cars, clients, rentals). Tenant-scoped.</summary>
    public const string AgencyStaff = nameof(AgencyStaff);
}
