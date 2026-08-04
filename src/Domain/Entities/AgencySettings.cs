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

        // ---------------------------------------------------------------------
        // Notifications. The lead times below are how far ahead the agency wants
        // to be warned; they are per-agency because a two-car outfit and a fifty-
        // car fleet do not plan on the same horizon.
        // ---------------------------------------------------------------------

        // Days of warning before a recurring car expense (maintenance, insurance,
        // technical inspection, road tax…) falls due by date.
        public int ExpenseDueLeadDays { get; set; } = 14;

        // Kilometres of warning before one falls due by odometer. Separate from
        // the days above because the two thresholds are independent: an oil
        // change comes due at whichever arrives first.
        public int ExpenseDueLeadKilometers { get; set; } = 1000;

        // Days of warning before a confirmed reservation starts.
        public int ReservationUpcomingLeadDays { get; set; } = 3;

        // Whether staff notifications are emailed as well. They are always
        // in-app; this only adds mail, so switching it off is quieting the inbox,
        // not losing the alert.
        public bool NotifyStaffByEmail { get; set; } = true;

        // Master switch for writing to clients at all. Off by default: an agency
        // opts in to mailing its customers, it is not opted in by an upgrade.
        public bool NotifyClientsByEmail { get; set; }

        // Days before a booking starts / ends to remind the client. Zero means
        // that particular reminder is off, which is why they are two settings and
        // not one window.
        public int ClientReminderDaysBeforeStart { get; set; } = 2;
        public int ClientReminderDaysBeforeEnd { get; set; } = 1;
    }
}
