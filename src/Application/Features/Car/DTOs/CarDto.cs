using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    public class CarDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public string Matricule { get; init; } = string.Empty;
        public string? ModelName { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        public string? ImageUrl { get; init; }
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Car, CarDto>()
                      .Map(dest => dest.ModelName, src => src.Model != null ? src.Model.Name : string.Empty);
            }
        }
    }
}
