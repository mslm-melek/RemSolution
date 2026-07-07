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
/// an audit row first.
/// </summary>
public class CrossTenantAccess : ICrossTenantAccess
{
    private readonly ApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;
    private readonly TimeProvider _dateTime;

    public CrossTenantAccess(
        ApplicationDbContext context,
        IUser user,
        IIdentityService identityService,
        TimeProvider dateTime)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
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

        // Raw INSERT so the audit row does not ride on the handler's change
        // tracker: it persists even if the surrounding operation aborts, and a
        // later SaveChanges cannot accidentally skip or duplicate it.
        await _context.Database.ExecuteSqlAsync($@"
INSERT INTO CrossTenantAccessLogs (UserId, Justification, OccurredOn)
VALUES ({userId}, {justification}, {_dateTime.GetUtcNow()});", cancellationToken);

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
