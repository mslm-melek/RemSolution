
namespace RemSolution.Application.Features.ModelCar.DTOs
{
    public class ModelCarDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? BrandId { get; init; }
        public string? BrandName { get; init; }
   
        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.ModelCar, ModelCarDto>()
                      .Map(dest => dest.BrandName, src => src.Brand != null ? src.Brand.Name : string.Empty);
            }
        }
    }
}
