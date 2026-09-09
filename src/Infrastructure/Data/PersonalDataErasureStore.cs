using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;

namespace RemSolution.Infrastructure.Data;

/// <summary>
/// <see cref="IPersonalDataErasureStore"/> over the concrete context. The only
/// place in the app that reads a client past the soft-delete filter, and the only
/// place that writes <c>AuditLog</c> outside the interceptor — see the interface
/// for why each is necessary. Pinned by <c>TenantEnforcementTests</c>.
/// </summary>
public sealed class PersonalDataErasureStore : IPersonalDataErasureStore
{
    private const string ClientEntity = nameof(Client);
    private const string ErasureAction = "ErasePersonalData";

    private readonly ApplicationDbContext _context;
    private readonly ITenantProvider _tenant;
    private readonly IUser _user;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _dateTime;

    public PersonalDataErasureStore(
        ApplicationDbContext context,
        ITenantProvider tenant,
        IUser user,
        ICorrelationContext correlation,
        TimeProvider dateTime)
    {
        _context = context;
        _tenant = tenant;
        _user = user;
        _correlation = correlation;
        _dateTime = dateTime;
    }

    public async Task<Client?> FindClientAsync(int clientId, CancellationToken cancellationToken)
    {
        // No tenant, nothing to find — the same answer the query filter gives
        // every other read, spelled out here because the filter is off.
        if (_tenant.AgencyId is not int agencyId) return null;

        return await _context.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.AgencyId == agencyId, cancellationToken);
    }

    public Task<List<Client>> ClientsPendingErasureAsync(
        int agencyId, CancellationToken cancellationToken) =>
        _context.Clients
            .IgnoreQueryFilters()
            .Where(c => c.AgencyId == agencyId && c.PersonalDataErasedAt == null)
            .ToListAsync(cancellationToken);

    public Task<int> RedactAuditTrailAsync(int clientId, CancellationToken cancellationToken)
    {
        var key = clientId.ToString();

        // ExecuteUpdate rather than loading the rows: a long-lived client can have
        // hundreds of audit rows, and materialising them would pull the very JSON
        // this exists to destroy back through the application. It also bypasses
        // the change tracker, so the audit interceptor cannot observe the
        // redaction and copy those payloads into fresh rows.
        return _context.Set<AuditLog>()
            .Where(a => a.Entity == ClientEntity && a.EntityId == key
                        && (a.Before != null || a.After != null))
            .ExecuteUpdateAsync(
                set => set.SetProperty(a => a.Before, (string?)null)
                          .SetProperty(a => a.After, (string?)null),
                cancellationToken);
    }

    public async Task RecordErasureAsync(
        int clientId, int agencyId, string? reason, CancellationToken cancellationToken)
    {
        _context.Set<AuditLog>().Add(new AuditLog
        {
            UserId = _user.Id,
            UserName = _user.UserName,
            AgencyId = agencyId,
            Action = ErasureAction,
            Entity = ClientEntity,
            EntityId = clientId.ToString(),
            // The operator's own note — a subject request, a retention rule — and
            // the only thing worth keeping beside the fact of the erasure. A
            // before/after payload here would undo the erasure it records.
            After = string.IsNullOrWhiteSpace(reason)
                ? null
                : JsonSerializer.Serialize(new { Reason = reason.Trim() }),
            CorrelationId = _correlation.CorrelationId,
            OccurredOn = _dateTime.GetUtcNow()
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
