using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Brand.DTOs;

namespace RemSolution.Application.Features.Brand.Queries.GetBrandsQuery
{
    public record GetBrandsQuery : IRequest<IList<BrandDto>>;

    public class GetBrandsQueryHandler : IRequestHandler<GetBrandsQuery, IList<BrandDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        public GetBrandsQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

        }
        public async Task<IList<BrandDto>> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
        {
            return await _context.Brands
                     .AsNoTracking()
                     .ProjectTo<BrandDto>(_mapper.ConfigurationProvider)
                     .ToListAsync(cancellationToken);
        }
    }
}
