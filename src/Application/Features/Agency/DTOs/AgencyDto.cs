
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Agency.DTOs
{
    public class AgencyDto
    {
        public int Id { get; init; }
        // Optimistic-concurrency token; echoed back on update (see P.8).
        public byte[]? RowVersion { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Address { get; init; }
        // The HQ pin, so the address can be shown back on a map.
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public int CountryId { get; init; }
        public string? CountryName { get; init; }
        // When the agency went live on the marketplace; null while it is still
        // being set up (see Agency.PublishedAt). An instant, rendered local.
        public DateTime? PublishedAt { get; init; }
        // Settings surfaced from the agency's AgencySettings row (see P.9).
        public string Currency { get; init; } = string.Empty;
        public int CancellationWindowHours { get; init; }
        public int ReservationExpiryHours { get; init; }
        // Notification settings, from the same row (see AgencySettings).
        public int ExpenseDueLeadDays { get; init; }
        public int ExpenseDueLeadKilometers { get; init; }
        public int ReservationUpcomingLeadDays { get; init; }
        public int ClientDocumentExpiryLeadDays { get; init; }
        public bool NotifyStaffByEmail { get; init; }
        public bool NotifyClientsByEmail { get; init; }
        public int ClientReminderDaysBeforeStart { get; init; }
        public int ClientReminderDaysBeforeEnd { get; init; }
        // Tax settings, from the same row. What the invoice needs to be a legal
        // document; see the tax section of AgencySettings.
        public string? TaxIdentifier { get; init; }
        public decimal VatRatePercent { get; init; }
        public decimal FiscalStampAmount { get; init; }
        // Default validity of each identity document, in years; zero means the
        // document does not expire in this jurisdiction.
        public int CINValidityYears { get; init; }
        public int PasseportValidityYears { get; init; }
        public int DrivingLicenceValidityYears { get; init; }
        // What calling a booking off costs the customer, and how far ahead it
        // stays free. See CancellationPolicy.
        public CancellationFeeMode CancellationFeeMode { get; init; }
        public decimal CancellationFeeValue { get; init; }
        public int CancellationFreeHours { get; init; }
        // Months after a client's last dealing before their personal data is
        // erased; zero means no automatic purge. See AgencySettings.
        public int PersonalDataRetentionMonths { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Agency, AgencyDto>()
                    .Map(d => d.CountryName, s => s.Country != null ? s.Country.Name : null)
                    // A Point is (X, Y): X is the longitude, Y the latitude.
                    .Map(d => d.Latitude, s => s.Location != null ? (double?)s.Location.Y : null)
                    .Map(d => d.Longitude, s => s.Location != null ? (double?)s.Location.X : null)
                    .Map(d => d.Currency, s => s.Settings != null ? s.Settings.CurrencyCode : string.Empty)
                    .Map(d => d.CancellationWindowHours, s => s.Settings != null ? s.Settings.CancellationWindowHours : 0)
                    .Map(d => d.ReservationExpiryHours, s => s.Settings != null ? s.Settings.ReservationExpiryHours : 0)
                    .Map(d => d.ExpenseDueLeadDays, s => s.Settings != null ? s.Settings.ExpenseDueLeadDays : 0)
                    .Map(d => d.ExpenseDueLeadKilometers, s => s.Settings != null ? s.Settings.ExpenseDueLeadKilometers : 0)
                    .Map(d => d.ReservationUpcomingLeadDays, s => s.Settings != null ? s.Settings.ReservationUpcomingLeadDays : 0)
                    .Map(d => d.ClientDocumentExpiryLeadDays, s => s.Settings != null ? s.Settings.ClientDocumentExpiryLeadDays : 0)
                    .Map(d => d.NotifyStaffByEmail, s => s.Settings != null && s.Settings.NotifyStaffByEmail)
                    .Map(d => d.NotifyClientsByEmail, s => s.Settings != null && s.Settings.NotifyClientsByEmail)
                    .Map(d => d.ClientReminderDaysBeforeStart, s => s.Settings != null ? s.Settings.ClientReminderDaysBeforeStart : 0)
                    .Map(d => d.ClientReminderDaysBeforeEnd, s => s.Settings != null ? s.Settings.ClientReminderDaysBeforeEnd : 0)
                    .Map(d => d.TaxIdentifier, s => s.Settings != null ? s.Settings.TaxIdentifier : null)
                    .Map(d => d.VatRatePercent, s => s.Settings != null ? s.Settings.VatRatePercent : 0m)
                    .Map(d => d.FiscalStampAmount, s => s.Settings != null ? s.Settings.FiscalStampAmount : 0m)
                    .Map(d => d.CINValidityYears, s => s.Settings != null ? s.Settings.CINValidityYears : 0)
                    .Map(d => d.PasseportValidityYears, s => s.Settings != null ? s.Settings.PasseportValidityYears : 0)
                    .Map(d => d.DrivingLicenceValidityYears, s => s.Settings != null ? s.Settings.DrivingLicenceValidityYears : 0)
                    .Map(d => d.CancellationFeeMode,
                         s => s.Settings != null ? s.Settings.CancellationFeeMode : CancellationFeeMode.None)
                    .Map(d => d.CancellationFeeValue, s => s.Settings != null ? s.Settings.CancellationFeeValue : 0m)
                    .Map(d => d.CancellationFreeHours, s => s.Settings != null ? s.Settings.CancellationFreeHours : 0)
                    .Map(d => d.PersonalDataRetentionMonths,
                         s => s.Settings != null ? s.Settings.PersonalDataRetentionMonths : 0);
            }
        }
    }
}
