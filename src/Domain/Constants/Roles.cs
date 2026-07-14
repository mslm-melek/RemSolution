namespace RemSolution.Domain.Constants;

public abstract class Roles
{
    /// <summary>
    /// Operates the platform: manages agencies, plans and subscriptions.
    /// Carries no AgencyId claim — cross-tenant reads only through the
    /// audited ICrossTenantAccess path.
    /// </summary>
    public const string PlatformAdministrator = nameof(PlatformAdministrator);

    /// <summary>
    /// Manages one agency (its staff, settings). Tenant-scoped. Holds every
    /// permission implicitly — the per-permission policies accept the role
    /// itself, no claims required.
    /// </summary>
    public const string AgencyAdministrator = nameof(AgencyAdministrator);

    /// <summary>
    /// Day-to-day agency work. Tenant-scoped. The role only labels the user
    /// as staff — what they can actually do is the set of permission claims
    /// they hold (see <see cref="Permissions"/>); no authorization is keyed
    /// on this role.
    /// </summary>
    public const string AgencyStaff = nameof(AgencyStaff);
}
