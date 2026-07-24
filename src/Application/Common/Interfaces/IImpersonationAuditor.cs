namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Writes the durable audit record for a platform administrator's read-only
/// cross-tenant browse (see the impersonation middleware). Reading another
/// tenant's data is security-sensitive, so every impersonated request leaves an
/// AuditLog row (Action "ImpersonatedRead") tied to the acting user, the target
/// agency and the request path — mirroring the trail written by the audited
/// <see cref="ICrossTenantAccess"/> path.
/// </summary>
public interface IImpersonationAuditor
{
    Task RecordAsync(int agencyId, string path, CancellationToken cancellationToken);
}
