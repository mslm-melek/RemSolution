using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

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
    // How far ahead to warn that a client's paperwork is running out.
    int ClientDocumentExpiryLeadDays = 30,
    // Staff alerts are always in-app; this only decides whether they are mailed.
    bool NotifyStaffByEmail = true,
    // Master switch for mailing clients, and the two lead times it governs. Zero
    // days switches that one reminder off without touching the other.
    bool NotifyClientsByEmail = false,
    int ClientReminderDaysBeforeStart = 2,
    int ClientReminderDaysBeforeEnd = 1,
    // What the invoice needs to be a legal document: the agency's tax number,
    // the rate to apply, and the flat duty stamp. Read here and then FROZEN onto
    // each Facture — see the tax section of the AgencySettings entity, and note
    // that stored amounts are tax-INCLUSIVE.
    string? TaxIdentifier = null,
    decimal VatRatePercent = 19m,
    decimal FiscalStampAmount = 1m,
    // Default validity of each identity document, used to fill in an expiry the
    // agent did not type. Zero means the document does not expire here.
    int CINValidityYears = 10,
    int PasseportValidityYears = 5,
    int DrivingLicenceValidityYears = 10,
    // What calling a booking off costs the customer, and how far ahead it is
    // still free. The arithmetic lives in CancellationPolicy, not here.
    CancellationFeeMode CancellationFeeMode = CancellationFeeMode.None,
    decimal CancellationFeeValue = 0m,
    int CancellationFreeHours = 48,
    // A second currency the invoice total is also shown in, informationally.
    // Null prints nothing. See AgencySettings for why this is not a billing
    // currency.
    string? InvoiceDisplayCurrency = null)
{
    /// <summary>The cancellation rules as the one object that applies them.</summary>
    public CancellationPolicy CancellationPolicy => new(
        CancellationFeeMode, CancellationFeeValue, CancellationFreeHours, CancellationWindowHours);
}

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
