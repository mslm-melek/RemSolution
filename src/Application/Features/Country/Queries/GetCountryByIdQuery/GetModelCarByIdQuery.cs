using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Country.DTOs;

namespace RemSolution.Application.Features.Country.Queries.GetCountryByIdQuery
{
    public record GetCountryByIdQuery(int Id) : IRequest<CountryDto?>;

    public class GetCountryByIdQueryHandler : IRequestHandler<GetCountryByIdQuery, CountryDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;


        public GetCountryByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

        }

        public async Task<CountryDto?> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken)
        {
            var country = await _context.Countries
              .Where(c => c.Id == request.Id)
              .ProjectTo<CountryDto>(_mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            if (country == null)
                throw new NotFoundException(nameof(Car), request.Id.ToString());

            return country;
        }

    }
}
