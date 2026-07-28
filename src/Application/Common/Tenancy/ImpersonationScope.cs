namespace RemSolution.Application.Common.Tenancy;

/// <summary>
/// Marks that the current request is a platform administrator working inside
/// another agency's workspace (see the web impersonation middleware). It is kept
/// deliberately separate from <see cref="AmbientTenant"/>: a background worker
/// legitimately pushes an ambient tenant to act as an agency, but it must never
/// gain the platform-admin permission bypass. Only while this flag is set does a
/// platform admin satisfy the impersonated tenant's permission policies — every
/// one of them, reads and writes alike, which is why the middleware audits each
/// request that opens the scope. Read by the per-permission authorization policy
/// and by the current-user endpoint (so the SPA is handed the same effective
/// permission set the API will enforce). Backed by <see cref="AsyncLocal{T}"/> so
/// the flag flows through the async call chain (including the rebuilt principal
/// inside AuthorizationBehaviour) and is isolated per logical thread.
/// </summary>
public static class ImpersonationScope
{
    private static readonly AsyncLocal<bool> Active = new();

    public static bool IsActive => Active.Value;

    public static IDisposable Begin()
    {
        var previous = Active.Value;
        Active.Value = true;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly bool _previous;

        public Scope(bool previous) => _previous = previous;

        public void Dispose() => Active.Value = _previous;
    }
}
