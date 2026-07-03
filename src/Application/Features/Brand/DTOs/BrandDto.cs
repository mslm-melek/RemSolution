
namespace RemSolution.Application.Features.Brand.DTOs
{

    public class BrandDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;

        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Brand, BrandDto>();
            }
        }
    }
}
