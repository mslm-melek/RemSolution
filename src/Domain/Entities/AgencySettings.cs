namespace RemSolution.Domain.Entities
{
    // Per-agency configuration in one dependent table (1:1 with Agency), so
    // adding a setting is a new column here rather than an ALTER TABLE on
    // Agencies. Created with the agency, cascade-deleted with it, and read
    // through the cached IAgencySettingsProvider — never queried ad hoc by
    // handlers on the hot path.
    public class AgencySettings : BaseAuditableEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        // ISO 4217 code the agency trades in; every Money amount it stores uses
        // it. Single-currency per tenant.
        public string CurrencyCode { get; set; } = "TND";

        // Hours from a booking's start within which a cancellation is allowed.
        public int CancellationWindowHours { get; set; } = 24;

        // Hours a pending reservation is held before it is considered expired.
        public int ReservationExpiryHours { get; set; } = 48;
    }
}
