
namespace RemSolution.Application.Features.Country.DTOs
{
    public class CountryDto
    {
        public string Name { get; init; } = string.Empty;
   
        public class Mapping : Profile
        {
            public Mapping()
            {
                CreateMap<Domain.Entities.Country, CountryDto>();
            }
        }
    }
}
