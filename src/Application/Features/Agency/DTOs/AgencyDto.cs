
namespace RemSolution.Application.Features.Agency.DTOs
{
    public class AgencyDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Address { get; init; }
        public int CountryId { get; init; }
        public string? CountryName { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Agency, AgencyDto>()
                    .Map(d => d.CountryName, s => s.Country != null ? s.Country.Name : null)
                    .Map(d => d.Latitude, s => s.Location != null ? (double?)s.Location.Y : null)
                    .Map(d => d.Longitude, s => s.Location != null ? (double?)s.Location.X : null);
            }
        }
    }
}
