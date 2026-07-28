using System.Text.Json;
using RemSolution.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Infrastructure.Data;

/// <summary>
/// Records a platform administrator's cross-tenant request as an AuditLog row.
/// Uses a raw INSERT (mirroring <see cref="CrossTenantAccess"/>) so the audit
/// row does not ride on the request's change tracker: it persists independently
/// of whatever the impersonated request does downstream — including a write that
/// then fails validation, which is exactly the attempt worth having on record.
/// </summary>
public class ImpersonationAuditor : IImpersonationAuditor
{
    /// <summary>AuditLog.Action for a platform-admin impersonated read (GET).</summary>
    public const string ImpersonatedReadAction = "ImpersonatedRead";

    /// <summary>AuditLog.Action for a platform-admin impersonated mutation.</summary>
    public const string ImpersonatedWriteAction = "ImpersonatedWrite";

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

    public async Task RecordAsync(int agencyId, string method, string path, CancellationToken cancellationToken)
    {
        var occurredOn = _dateTime.GetUtcNow();
        var payload = JsonSerializer.Serialize(new { Method = method, Path = path });

        // A GET only looks; anything else changes the agency's data. Splitting
        // the action makes the mutations findable in a trail that is otherwise
        // dominated by the reads a browsing session generates.
        var action = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            ? ImpersonatedReadAction
            : ImpersonatedWriteAction;

        await _context.Database.ExecuteSqlAsync($@"
INSERT INTO AuditLogs (UserId, UserName, AgencyId, Action, Entity, EntityId, Before, After, CorrelationId, OccurredOn)
VALUES ({_user.Id}, {_user.UserName}, {agencyId}, {action}, {string.Empty}, NULL, NULL, {payload}, {_correlation.CorrelationId}, {occurredOn});", cancellationToken);
    }
}
