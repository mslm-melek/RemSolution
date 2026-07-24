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

    public static int? CurrentAgencyId => Current.Value;

    public static IDisposable Push(int agencyId)
    {
        var previous = Current.Value;
        Current.Value = agencyId;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly int? _previous;

        public Scope(int? previous) => _previous = previous;

        public void Dispose() => Current.Value = _previous;
    }
}
