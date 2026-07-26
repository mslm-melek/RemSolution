using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Enums;

namespace RemSolution.Infrastructure.Jobs;

// Recurring sweep that lapses Pending reservations whose ExpiresAt has passed.
// It runs with no HTTP context and must touch every agency, so it can't lean on
// a single ambient tenant — and deliberately does NOT bypass the tenant query
// filter (that bypass is pinned to the sanctioned cross-tenant paths by the
// convention tests). Instead it enumerates agencies (Agency is not
// tenant-scoped) and processes each under its own AmbientTenant.Push, so the
// tenant query filter and the write-stamp behave exactly as in a request.
public sealed class ReservationExpiryJob
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReservationExpiryJob> _logger;

    public ReservationExpiryJob(
        IApplicationDbContext context,
        TimeProvider timeProvider,
        ILogger<ReservationExpiryJob> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var agencyIds = await _context.Agencies.Select(a => a.Id).ToListAsync();

        foreach (var agencyId in agencyIds)
        {
            using var _ = AmbientTenant.Push(agencyId);

            var stale = await _context.Reservations
                .Where(r => r.Status == ReservationStatus.PendingConfirmation
                            && r.ExpiresAt != null
                            && r.ExpiresAt <= now)
                .ToListAsync();

            if (stale.Count == 0)
            {
                continue;
            }

            foreach (var reservation in stale)
            {
                reservation.Expire();
            }

            await _context.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation(
                "Expired {Count} reservation(s) for agency {AgencyId}", stale.Count, agencyId);
        }
    }
}
