using System.Text.Json;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Common;
using RemSolution.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Infrastructure.Data;

/// <summary>
/// The single implementation of the audited cross-tenant read path. This is
/// the only place outside the marketplace search feature where
/// IgnoreQueryFilters is permitted (pinned by TenantEnforcementTests): the
/// bypass is gated on the PlatformAdministrator role and every access writes
/// its audit rows first.
///
/// Cross-tenant reads are part of the [Auditable] trail by contract: the
/// calling request must carry the marker (pinned by CrossTenantAuditTests,
/// enforced here at runtime), and each access writes BOTH audit rows — the
/// CrossTenantAccessLog (the dedicated access register) and an AuditLog row
/// (Action "CrossTenantRead"), so the read shows up in the same trail as the
/// business action it belongs to, tied together by the correlation id.
/// </summary>
public class CrossTenantAccess : ICrossTenantAccess
{
    /// <summary>AuditLog.Action for the cross-tenant read rows written here.</summary>
    public const string CrossTenantReadAction = "CrossTenantRead";

    private readonly ApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;
    private readonly IAuditScope _auditScope;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _dateTime;

    public CrossTenantAccess(
        ApplicationDbContext context,
        IUser user,
        IIdentityService identityService,
        IAuditScope auditScope,
        ICorrelationContext correlation,
        TimeProvider dateTime)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
        _auditScope = auditScope;
        _correlation = correlation;
        _dateTime = dateTime;
    }

    public async Task<ICrossTenantScope> BeginAuditedAccessAsync(string justification, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(justification);

        if (_user.Id is not string userId ||
            !await _identityService.IsInRoleAsync(userId, Roles.PlatformAdministrator))
        {
            throw new ForbiddenAccessException();
        }

        // The audit intent is opened by the AuditableBehaviour, so a null here
        // means the calling request is not marked [Auditable] — refuse rather
        // than leave a hole in the business trail.
        if (_auditScope.Current is not AuditIntent intent)
        {
            throw new InvalidOperationException(
                "Cross-tenant reads are auditable by contract: the calling request must be marked [Auditable].");
        }

        var occurredOn = _dateTime.GetUtcNow();

        // The AuditLog row is a read record: no before/after state to capture,
        // so the payload documents why the bypass happened and for which
        // business action.
        var payload = JsonSerializer.Serialize(new
        {
            Justification = justification,
            RequestedBy = intent.Action,
        });

        // Raw INSERTs so the audit rows do not ride on the handler's change
        // tracker: they persist even if the surrounding operation aborts, and a
        // later SaveChanges cannot accidentally skip or duplicate them.
        await _context.Database.ExecuteSqlAsync($@"
INSERT INTO CrossTenantAccessLogs (UserId, Justification, OccurredOn)
VALUES ({userId}, {justification}, {occurredOn});
INSERT INTO AuditLogs (UserId, UserName, AgencyId, Action, Entity, EntityId, Before, After, CorrelationId, OccurredOn)
VALUES ({userId}, {_user.UserName}, NULL, {CrossTenantReadAction}, {intent.Entity ?? string.Empty}, NULL, NULL, {payload}, {_correlation.CorrelationId}, {occurredOn});", cancellationToken);

        return new CrossTenantScope(_context);
    }

    private sealed class CrossTenantScope : ICrossTenantScope
    {
        private readonly ApplicationDbContext _context;

        public CrossTenantScope(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<TEntity> Query<TEntity>() where TEntity : class, ITenantEntity
            => _context.Set<TEntity>().IgnoreQueryFilters().AsNoTracking();
    }
}
