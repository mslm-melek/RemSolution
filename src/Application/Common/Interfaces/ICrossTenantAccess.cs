using RemSolution.Domain.Common;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// The dedicated, audited path for platform administrators to read tenant data
/// across agencies (tenant query filters bypassed). The caller must hold the
/// PlatformAdministrator role — anyone else gets ForbiddenAccessException —
/// and the audit rows (a CrossTenantAccessLog plus an AuditLog row with action
/// "CrossTenantRead") are persisted before any data is exposed, regardless of
/// whether the surrounding operation completes. The calling request MUST be
/// marked <c>[Auditable]</c> — cross-tenant reads are part of the business
/// audit trail by contract (pinned by CrossTenantAuditTests, refused at
/// runtime otherwise). Read-only: the scope hands out untracked queries,
/// never write access.
/// </summary>
public interface ICrossTenantAccess
{
    Task<ICrossTenantScope> BeginAuditedAccessAsync(string justification, CancellationToken cancellationToken);
}

public interface ICrossTenantScope
{
    IQueryable<TEntity> Query<TEntity>() where TEntity : class, ITenantEntity;
}
