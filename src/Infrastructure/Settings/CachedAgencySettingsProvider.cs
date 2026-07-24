using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Settings;

namespace RemSolution.Infrastructure.Settings;

// Reads an agency's settings once and caches the snapshot; settings change
// rarely (only via UpdateAgencyCommand, which invalidates the entry). Scoped so
// it uses the request's DbContext; the IMemoryCache it writes to is a singleton
// shared across requests.
public sealed class CachedAgencySettingsProvider : IAgencySettingsProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public CachedAgencySettingsProvider(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<AgencySettingsSnapshot> GetAsync(int agencyId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(Key(agencyId), out AgencySettingsSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        var snapshot = await _context.AgencySettings
            .Where(s => s.AgencyId == agencyId)
            .Select(s => new AgencySettingsSnapshot(
                s.CurrencyCode, s.CancellationWindowHours, s.ReservationExpiryHours))
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            throw new InvalidOperationException($"Agency {agencyId} has no settings configured.");
        }

        _cache.Set(Key(agencyId), snapshot, Ttl);
        return snapshot;
    }

    public void Invalidate(int agencyId) => _cache.Remove(Key(agencyId));

    private static string Key(int agencyId) => $"agency-settings:{agencyId}";
}
