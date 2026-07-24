namespace RemSolution.Application.Common.Settings;

/// <summary>
/// Immutable read view of an agency's <see cref="Domain.Entities.AgencySettings"/>.
/// </summary>
public sealed record AgencySettingsSnapshot(
    string CurrencyCode,
    int CancellationWindowHours,
    int ReservationExpiryHours);

/// <summary>
/// The single read path for per-agency settings. Settings change rarely and are
/// read on hot paths (e.g. currency on every priced write), so the provider
/// caches each agency's snapshot; commands that change settings call
/// <see cref="Invalidate"/> so the next read reloads. Not query-filtered —
/// <see cref="Domain.Entities.Agency"/> is not an ITenantEntity, so callers pass
/// their own agency id.
/// </summary>
public interface IAgencySettingsProvider
{
    Task<AgencySettingsSnapshot> GetAsync(int agencyId, CancellationToken cancellationToken = default);

    void Invalidate(int agencyId);
}
