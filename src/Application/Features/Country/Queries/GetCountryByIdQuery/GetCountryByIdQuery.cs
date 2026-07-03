using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Country.DTOs;

namespace RemSolution.Application.Features.Country.Queries.GetCountryByIdQuery
{
    public record GetCountryByIdQuery(int Id) : IRequest<CountryDto?>;

    public class GetCountryByIdQueryHandler : IRequestHandler<GetCountryByIdQuery, CountryDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetCountryByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CountryDto?> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken)
        {
            var country = await _context.Countries
              .Where(c => c.Id == request.Id)
              .ProjectToType<CountryDto>()
              .FirstOrDefaultAsync(cancellationToken);

            if (country == null)
                throw new NotFoundException(nameof(Domain.Entities.Country), request.Id.ToString());

            return country;
        }

    }
}
