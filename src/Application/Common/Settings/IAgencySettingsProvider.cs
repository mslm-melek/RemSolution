namespace RemSolution.Application.Common.Settings;

/// <summary>
/// Immutable read view of an agency's <see cref="Domain.Entities.AgencySettings"/>.
/// </summary>
public sealed record AgencySettingsSnapshot(
    string CurrencyCode,
    int CancellationWindowHours,
    int ReservationExpiryHours,
    // How far ahead the agency wants to hear about a recurring car cost coming
    // due, on each of the two clocks (see ExpenseDueCalculator).
    int ExpenseDueLeadDays = 14,
    int ExpenseDueLeadKilometers = 1000,
    int ReservationUpcomingLeadDays = 3,
    // Staff alerts are always in-app; this only decides whether they are mailed.
    bool NotifyStaffByEmail = true,
    // Master switch for mailing clients, and the two lead times it governs. Zero
    // days switches that one reminder off without touching the other.
    bool NotifyClientsByEmail = false,
    int ClientReminderDaysBeforeStart = 2,
    int ClientReminderDaysBeforeEnd = 1);

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
