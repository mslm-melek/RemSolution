
namespace RemSolution.Application.Features.Country.DTOs
{
    public class CountryDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
   
        public class Mapping : IRegister
        {
            public void Register(TypeAdapterConfig config)
            {
                config.NewConfig<Domain.Entities.Country, CountryDto>();
            }
        }
    }
}
