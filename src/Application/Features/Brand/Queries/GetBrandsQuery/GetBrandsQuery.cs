using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Brand.DTOs;

namespace RemSolution.Application.Features.Brand.Queries.GetBrandsQuery
{
    public record GetBrandsQuery : IRequest<IList<BrandDto>>;

    public class GetBrandsQueryHandler : IRequestHandler<GetBrandsQuery, IList<BrandDto>>
    {
        private readonly IApplicationDbContext _context;
        public GetBrandsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IList<BrandDto>> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
        {
            return await _context.Brands.OrderBy(a=>a.Name)
                     .AsNoTracking()
                     .ProjectToType<BrandDto>()
                     .ToListAsync(cancellationToken);
        }
    }
}
