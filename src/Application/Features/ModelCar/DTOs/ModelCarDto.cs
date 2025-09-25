
namespace RemSolution.Application.Features.ModelCar.DTOs
{
    public class ModelCarDto
    {
        public string Name { get; init; } = string.Empty;
        public string? BrandName { get; init; }
   
        public class Mapping : Profile
        {
            public Mapping()
            {
                CreateMap<Domain.Entities.ModelCar, ModelCarDto>()
                              .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand != null ? src.Brand.Name : string.Empty));
            }
        }
    }
}
