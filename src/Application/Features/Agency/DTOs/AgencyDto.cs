
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
        public int CountryId { get; init; }
        public string? CountryName { get; init; }
        // Settings surfaced from the agency's AgencySettings row (see P.9).
        public string Currency { get; init; } = string.Empty;
        public int CancellationWindowHours { get; init; }
        public int ReservationExpiryHours { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Agency, AgencyDto>()
                    .Map(d => d.CountryName, s => s.Country != null ? s.Country.Name : null)
                    .Map(d => d.Currency, s => s.Settings != null ? s.Settings.CurrencyCode : string.Empty)
                    .Map(d => d.CancellationWindowHours, s => s.Settings != null ? s.Settings.CancellationWindowHours : 0)
                    .Map(d => d.ReservationExpiryHours, s => s.Settings != null ? s.Settings.ReservationExpiryHours : 0);
            }
        }
    }
}
