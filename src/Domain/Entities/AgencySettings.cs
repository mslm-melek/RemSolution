using RemSolution.Domain.Enums;

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

        // The hard cutoff: once a booking is within this many hours of its start
        // it can no longer be cancelled at all (see the cancel commands).
        public int CancellationWindowHours { get; set; } = 24;

        // ---------------------------------------------------------------------
        // Cancellation fees. Distinct from the cutoff above: that one says when
        // cancelling stops being possible, these say when it stops being free.
        // Cancelling earlier than CancellationFreeHours before the start costs
        // nothing; between there and the cutoff it costs the fee. See
        // CancellationPolicy, which owns the arithmetic.
        // ---------------------------------------------------------------------

        // Off by default: an agency opts into charging its customers.
        public CancellationFeeMode CancellationFeeMode { get; set; } = CancellationFeeMode.None;

        // A flat amount in the agency's currency, or a percentage — the mode says
        // which. Meaningless while the mode is None.
        public decimal CancellationFeeValue { get; set; }

        // How far ahead a customer can call a booking off for free. Should sit at
        // or above CancellationWindowHours, or the band that charges is empty.
        public int CancellationFreeHours { get; set; } = 48;

        // Hours a pending reservation is held before it is considered expired.
        public int ReservationExpiryHours { get; set; } = 48;

        // ---------------------------------------------------------------------
        // Tax. An invoice that does not carry the agency's tax number, the rate
        // applied, and the split between net and tax is not an invoice a
        // professional client can deduct or an accountant can file — so these
        // three are what turn the generated PDF into a legal document.
        //
        // THE DAILY RATE AN AGENCY ENTERS IS TAX-INCLUSIVE. Car.DailyRate,
        // Renting.Price and every fee are gross; the invoice works backwards to
        // the net amount (see FactureTax). This is deliberate — it is what staff
        // quote at the counter — and it is not a setting, because flipping it
        // would silently reinterpret every amount already stored.
        // ---------------------------------------------------------------------

        // The agency's tax registration number, printed on every invoice
        // ("matricule fiscal" in Tunisia). Null until the agency fills it in;
        // the invoice then prints nothing rather than a wrong number.
        public string? TaxIdentifier { get; set; }

        // VAT rate as a percentage — 19 means 19%. Frozen onto each invoice at
        // issue, because a rate is changed by law and an issued invoice must keep
        // saying what was charged. 19% is the standard Tunisian rate.
        public decimal VatRatePercent { get; set; } = 19m;

        // The fixed duty stamp added to the invoice total ("timbre fiscal"), in
        // the agency's currency. A flat amount, not a rate, and zero for a
        // jurisdiction that has no such thing.
        public decimal FiscalStampAmount { get; set; } = 1m;

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

        // How long each identity document stays valid, in years, used to fill in
        // an expiry date the agent did not type (see
        // Client.ApplyDefaultDocumentExpiries). Per agency because the answer is
        // the issuing country's, not ours, and the defaults below are Tunisia's.
        // Zero means "does not expire here", and derives nothing.
        public int CINValidityYears { get; set; } = 10;
        public int PasseportValidityYears { get; set; } = 5;
        public int DrivingLicenceValidityYears { get; set; } = 10;

        // Days of warning before a client's CIN, passport or driving licence
        // expires. A month by default, and deliberately longer than the other
        // lead times: renewing a licence is an appointment at an administration,
        // not an errand, and the point is to ask the client before the day they
        // turn up to collect a car.
        public int ClientDocumentExpiryLeadDays { get; set; } = 30;

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
