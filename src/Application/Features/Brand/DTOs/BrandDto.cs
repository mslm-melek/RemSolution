
namespace RemSolution.Application.Features.Brand.DTOs
{

    public class BrandDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;

        public class Mapping : Profile
        {
            public Mapping()
            {
                CreateMap<Domain.Entities.Brand, BrandDto>();
            }
        }
    }
}
