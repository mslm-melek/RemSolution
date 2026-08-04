
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
        // Settings surfaced from the agency's AgencySettings row (see P.9).
        public string Currency { get; init; } = string.Empty;
        public int CancellationWindowHours { get; init; }
        public int ReservationExpiryHours { get; init; }
        // Notification settings, from the same row (see AgencySettings).
        public int ExpenseDueLeadDays { get; init; }
        public int ExpenseDueLeadKilometers { get; init; }
        public int ReservationUpcomingLeadDays { get; init; }
        public bool NotifyStaffByEmail { get; init; }
        public bool NotifyClientsByEmail { get; init; }
        public int ClientReminderDaysBeforeStart { get; init; }
        public int ClientReminderDaysBeforeEnd { get; init; }

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
                    .Map(d => d.NotifyStaffByEmail, s => s.Settings != null && s.Settings.NotifyStaffByEmail)
                    .Map(d => d.NotifyClientsByEmail, s => s.Settings != null && s.Settings.NotifyClientsByEmail)
                    .Map(d => d.ClientReminderDaysBeforeStart, s => s.Settings != null ? s.Settings.ClientReminderDaysBeforeStart : 0)
                    .Map(d => d.ClientReminderDaysBeforeEnd, s => s.Settings != null ? s.Settings.ClientReminderDaysBeforeEnd : 0);
            }
        }
    }
}
