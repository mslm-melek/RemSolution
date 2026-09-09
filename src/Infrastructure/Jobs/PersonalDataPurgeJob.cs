using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Client;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Data;

namespace RemSolution.Infrastructure.Jobs;

/// <summary>
/// The retention rule, applied: erases the personal data of clients an agency has
/// not dealt with for <c>AgencySettings.PersonalDataRetentionMonths</c>. Off for
/// any agency that has not set a number — see that setting for why no default was
/// invented here.
/// <para>
/// Daily rather than hourly. A retention window is measured in months, so the
/// hour it lands on carries no meaning, and this is the one sweep that destroys
/// data rather than reporting on it.
/// </para>
/// <para>
/// Like the other sweeps it runs with no HTTP context and processes each agency
/// under its own <see cref="AmbientTenant"/> push, so the tenant filter behaves
/// exactly as in a request. Archived clients — the ones whose scans nobody will
/// ever look at again — come from <see cref="IPersonalDataErasureStore"/>, which
/// owns that one lifted filter; nothing here bypasses anything.
/// </para>
/// </summary>
public sealed class PersonalDataPurgeJob
{
    private readonly IApplicationDbContext _context;
    private readonly IPersonalDataErasureStore _store;
    private readonly IStoredFileService _storedFiles;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PersonalDataPurgeJob> _logger;

    public PersonalDataPurgeJob(
        IApplicationDbContext context,
        IPersonalDataErasureStore store,
        IStoredFileService storedFiles,
        TimeProvider timeProvider,
        ILogger<PersonalDataPurgeJob> logger)
    {
        _context = context;
        _store = store;
        _storedFiles = storedFiles;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        var now = _timeProvider.GetUtcNow();

        // One query rather than a cached lookup per agency: this is a daily job
        // walking every tenant, and most agencies have the rule switched off.
        var policies = await _context.AgencySettings
            .Where(s => s.PersonalDataRetentionMonths > 0)
            .Select(s => new { s.AgencyId, s.PersonalDataRetentionMonths })
            .ToListAsync();

        foreach (var policy in policies)
        {
            using var _ = AmbientTenant.Push(policy.AgencyId);

            var cutoff = now.UtcDateTime.AddMonths(-policy.PersonalDataRetentionMonths);

            var erased = 0;

            foreach (var client in await DueAsync(policy.AgencyId, cutoff))
            {
                // Re-checked per client rather than filtered in SQL: the states
                // that count as "in the middle of something" live in one place
                // (see ClientPersonalDataErasure), and duplicating them as a
                // second predicate here is how the two would drift apart.
                if (await ClientPersonalDataErasure.HasLiveBookingAsync(
                        _context, client.Id, CancellationToken.None))
                {
                    continue;
                }

                await ClientPersonalDataErasure.EraseAsync(
                    _context, _storedFiles, _store, client, now,
                    $"Retention: {policy.PersonalDataRetentionMonths} month(s)",
                    CancellationToken.None);

                erased++;
            }

            if (erased > 0)
            {
                _logger.LogInformation(
                    "Erased personal data of {Count} client(s) for agency {AgencyId}",
                    erased, policy.AgencyId);
            }
        }
    }

    /// <summary>
    /// Clients whose last dealing with the agency is older than the cutoff and
    /// whose data has not already been erased — archived ones included.
    /// <para>
    /// "Last dealing" is the latest of the client's own hires (as renter or second
    /// driver), reservations and payments, falling back to when the record was
    /// created. Folded in memory from four cheap reads instead of one query with
    /// three correlated MAXes: an agency's client list is thousands of rows at
    /// most, and the SQL version was unreadable.
    /// </para>
    /// </summary>
    private async Task<List<Client>> DueAsync(int agencyId, DateTime cutoff)
    {
        var candidates = await _store.ClientsPendingErasureAsync(agencyId, CancellationToken.None);

        if (candidates.Count == 0) return candidates;

        var lastHire = await _context.Rentings
            .Where(r => r.ClientId != null)
            .GroupBy(r => r.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Last = g.Max(r => r.EndDate ?? r.StartDate) })
            .ToDictionaryAsync(x => x.ClientId, x => x.Last);

        var lastSecondHire = await _context.Rentings
            .Where(r => r.SecondClientId != null)
            .GroupBy(r => r.SecondClientId!.Value)
            .Select(g => new { ClientId = g.Key, Last = g.Max(r => r.EndDate ?? r.StartDate) })
            .ToDictionaryAsync(x => x.ClientId, x => x.Last);

        var lastReservation = await _context.Reservations
            .Where(r => r.ClientId != null)
            .GroupBy(r => r.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Last = g.Max(r => r.EndDate ?? r.StartDate) })
            .ToDictionaryAsync(x => x.ClientId, x => x.Last);

        var lastPayment = await _context.Payments
            .Where(p => p.ClientId != null && p.PayementDate != null)
            .GroupBy(p => p.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Last = g.Max(p => p.PayementDate) })
            .ToDictionaryAsync(x => x.ClientId, x => x.Last);

        return candidates
            .Where(c => LastDealing(c, lastHire, lastSecondHire, lastReservation, lastPayment) < cutoff)
            .ToList();
    }

    private static DateTime LastDealing(
        Client client,
        IReadOnlyDictionary<int, DateTime?> hires,
        IReadOnlyDictionary<int, DateTime?> secondHires,
        IReadOnlyDictionary<int, DateTime?> reservations,
        IReadOnlyDictionary<int, DateTime?> payments)
    {
        // A client with no booking at all is dated by their own record, so a row
        // typed in by mistake three years ago is still reached by the rule. A
        // missing stamp (seeded or imported data) counts as the beginning of time
        // rather than as "recent" — the bookings above are then what decides.
        var latest = client.CreatedOn?.UtcDateTime ?? DateTime.MinValue;

        foreach (var source in new[] { hires, secondHires, reservations, payments })
        {
            if (source.TryGetValue(client.Id, out var when) && when is DateTime date && date > latest)
            {
                latest = date;
            }
        }

        return latest;
    }
}
