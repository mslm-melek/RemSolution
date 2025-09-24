
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Brand.DTOs;

namespace RemSolution.Application.Features.Brand.Queries.GetBrandByIdQuery
{
    public record GetBrandByIdQuery(int Id) : IRequest<BrandDto?>;

    public class GetBrandByIdQueryHandler : IRequestHandler<GetBrandByIdQuery, BrandDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;


        public GetBrandByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

        }

        public async Task<BrandDto?> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
        {
            var brand = await _context.Brands
              .Where(c => c.Id == request.Id)
              .ProjectTo<BrandDto>(_mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            if (brand == null)
                throw new NotFoundException(nameof(Brand), request.Id.ToString());

            return brand;
        }
    }
}
