
namespace RemSolution.Application.Features.Branch.DTOs
{
    public class BranchDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public string? CountryName { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Branch, BranchDto>()
                    .Map(d => d.CountryName, s => s.Country != null ? s.Country.Name : null)
                    .Map(d => d.Latitude, s => s.Location != null ? (double?)s.Location.Y : null)
                    .Map(d => d.Longitude, s => s.Location != null ? (double?)s.Location.X : null);
            }
        }
    }
}
