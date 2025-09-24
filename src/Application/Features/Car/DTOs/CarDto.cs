using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    public class CarDto
    {
        public string Matricule { get; init; } = string.Empty;
        public string? ModelName { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        public string? ImageUrl { get; init; }
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
        public class Mapping : Profile
        {
            public Mapping()
            {
                CreateMap<Domain.Entities.Car, CarDto>()
                              .ForMember(dest => dest.ModelName, opt => opt.MapFrom(src => src.Model != null ? src.Model.Name : string.Empty));
            }
        }
    }
}
