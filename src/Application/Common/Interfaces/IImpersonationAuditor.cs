namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Writes the durable audit record for a platform administrator acting inside
/// another agency's workspace (see the impersonation middleware). Reaching into
/// a tenant is security-sensitive — and since the workspace allows writes, not
/// just reads, this trail is the main thing bounding it: every impersonated
/// request leaves an AuditLog row tied to the acting user, the target agency,
/// the HTTP verb and the request path. Reads are recorded as "ImpersonatedRead"
/// and everything else as "ImpersonatedWrite", so a write can be found without
/// reading through a whole session's worth of GETs. Mirrors the trail written by
/// the audited <see cref="ICrossTenantAccess"/> path.
/// </summary>
public interface IImpersonationAuditor
{
    Task RecordAsync(int agencyId, string method, string path, CancellationToken cancellationToken);
}
