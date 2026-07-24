using System.Text.Json;
using RemSolution.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Infrastructure.Data;

/// <summary>
/// Records a platform administrator's read-only cross-tenant browse as an
/// AuditLog row. Uses a raw INSERT (mirroring <see cref="CrossTenantAccess"/>)
/// so the audit row does not ride on the request's change tracker: it persists
/// independently of whatever the impersonated read does downstream.
/// </summary>
public class ImpersonationAuditor : IImpersonationAuditor
{
    /// <summary>AuditLog.Action for a platform-admin impersonated read.</summary>
    public const string ImpersonatedReadAction = "ImpersonatedRead";

    private readonly ApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _dateTime;

    public ImpersonationAuditor(
        ApplicationDbContext context,
        IUser user,
        ICorrelationContext correlation,
        TimeProvider dateTime)
    {
        _context = context;
        _user = user;
        _correlation = correlation;
        _dateTime = dateTime;
    }

    public async Task RecordAsync(int agencyId, string path, CancellationToken cancellationToken)
    {
        var occurredOn = _dateTime.GetUtcNow();
        var payload = JsonSerializer.Serialize(new { Path = path });

        await _context.Database.ExecuteSqlAsync($@"
INSERT INTO AuditLogs (UserId, UserName, AgencyId, Action, Entity, EntityId, Before, After, CorrelationId, OccurredOn)
VALUES ({_user.Id}, {_user.UserName}, {agencyId}, {ImpersonatedReadAction}, {string.Empty}, NULL, NULL, {payload}, {_correlation.CorrelationId}, {occurredOn});", cancellationToken);
    }
}
