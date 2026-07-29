namespace RemSolution.Application.Common.Tenancy;

/// <summary>
/// An ambient tenant override for work that runs outside an HTTP request, where
/// there is no AgencyId claim to read — chiefly the image-processing background
/// service. A worker calls <see cref="Push"/> with the agency it is acting for
/// before resolving tenant-scoped services; <see cref="ITenantProvider"/>
/// implementations consult <see cref="CurrentAgencyId"/> first, so query filters
/// and the tenant write-stamp behave exactly as they do in a request. Backed by
/// <see cref="AsyncLocal{T}"/>, so the value flows through the async call chain
/// and is isolated per logical thread.
/// </summary>
public static class AmbientTenant
{
    private static readonly AsyncLocal<int?> Current = new();
    private static readonly AsyncLocal<bool> Administrative = new();

    public static int? CurrentAgencyId => Current.Value;

    /// <summary>
    /// True while the ambient tenant was pushed by <see cref="PushAdministrative"/>
    /// — the app owner acting on an agency, rather than the agency working.
    /// </summary>
    public static bool CurrentIsAdministrative => Administrative.Value;

    public static IDisposable Push(int agencyId) => Push(agencyId, administrative: false);

    /// <summary>
    /// Acts as <paramref name="agencyId"/> for platform-administrator work that
    /// sets an agency up rather than uses it — creating an agency's branches with
    /// it, or fixing them afterwards.
    /// <para>
    /// Identical to <see cref="Push"/> except that it exempts the writes inside
    /// it from subscription enforcement, which exists to stop an agency changing
    /// its own data once it stops paying (see SubscriptionEnforcementInterceptor,
    /// whose contract is that platform-admin writes pass through untouched). Two
    /// cases need the exemption and neither is the agency's own work: a
    /// brand-new agency has no subscription yet — one is assigned after it
    /// exists — and an agency whose subscription has lapsed is precisely one the
    /// app owner may still have to administer.
    /// </para>
    /// <para>
    /// Tenant isolation is NOT relaxed: TenantEntityInterceptor still stamps
    /// inserts and refuses to touch another agency's rows, and query filters
    /// still scope reads. Only the billing gate is lifted.
    /// </para>
    /// </summary>
    public static IDisposable PushAdministrative(int agencyId) => Push(agencyId, administrative: true);

    private static IDisposable Push(int agencyId, bool administrative)
    {
        var scope = new Scope(Current.Value, Administrative.Value);

        Current.Value = agencyId;
        Administrative.Value = administrative;

        return scope;
    }

    private sealed class Scope : IDisposable
    {
        private readonly int? _previousAgencyId;
        private readonly bool _previousAdministrative;

        public Scope(int? previousAgencyId, bool previousAdministrative)
        {
            _previousAgencyId = previousAgencyId;
            _previousAdministrative = previousAdministrative;
        }

        public void Dispose()
        {
            Current.Value = _previousAgencyId;
            Administrative.Value = _previousAdministrative;
        }
    }
}
