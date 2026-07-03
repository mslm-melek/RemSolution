using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Country.DTOs;

namespace RemSolution.Application.Features.Country.Queries.GetCountriesQuery
{
    public record GetCountriesQuery : IRequest<IList<CountryDto>>;

    public class GetCountriesQueryHandler : IRequestHandler<GetCountriesQuery, IList<CountryDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetCountriesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<CountryDto>> Handle(GetCountriesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Countries
                .AsNoTracking()
                .ProjectToType<CountryDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
