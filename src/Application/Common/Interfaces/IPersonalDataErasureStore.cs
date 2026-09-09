using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// The parts of an erasure that an ordinary read cannot do.
/// <para>
/// Two of them, and each is here for its own reason:
/// </para>
/// <para>
/// 1. FINDING THE CLIENT. Every read of <c>Clients</c> is filtered to the tenant
/// AND to <c>!IsDeleted</c>, so an archived client is invisible — and an archived
/// client is precisely the one whose passport scan nobody will ever look at
/// again. Both lookups here see past the soft-delete half of that filter and
/// re-state <c>AgencyId</c> by hand, so the tenant boundary still holds.
/// </para>
/// <para>
/// 2. THE AUDIT TRAIL. Every <c>[Auditable]</c> command that touched a client
/// wrote its before/after state into <c>AuditLog</c> as JSON, so the last edit of
/// a client carries a full copy of their passport number. An erasure that left
/// those rows alone would not be an erasure. <c>AuditLog</c> is deliberately
/// absent from <see cref="IApplicationDbContext"/> — audit rows are the
/// interceptor's to write, not a handler's — and this is the one sanctioned way
/// in. It only blanks payloads and appends; it never deletes a row.
/// </para>
/// </summary>
public interface IPersonalDataErasureStore
{
    /// <summary>
    /// One client of the ambient tenant by id, archived or not, tracked by the
    /// same context the caller saves through. Null when there is no such client
    /// in this agency.
    /// </summary>
    Task<Client?> FindClientAsync(int clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Every client of the agency whose personal data has not been erased yet,
    /// archived ones included — the retention sweep's candidate list, before it
    /// works out which ones are past their window.
    /// </summary>
    Task<List<Client>> ClientsPendingErasureAsync(int agencyId, CancellationToken cancellationToken);

    /// <summary>
    /// Blanks the before/after payloads of every audit row about this client,
    /// keeping who did what and when. The trail survives; the data in it does
    /// not. Returns how many rows were redacted.
    /// </summary>
    Task<int> RedactAuditTrailAsync(int clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Appends the row saying the erasure happened. Carries no before/after by
    /// design — the reason is a note the operator typed, not a copy of what was
    /// erased.
    /// </summary>
    Task RecordErasureAsync(
        int clientId, int agencyId, string? reason, CancellationToken cancellationToken);
}
