using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.ModelCar.DTOs;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.ModelCar.Queries.GetModelCarsWithPaginationQuery
{
    public record GetModelCarsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? BrandId = null
    ) : IRequest<PaginatedList<ModelCarDto>>;
    public class GetModelCarsWithPaginationQueryHandler
        : IRequestHandler<GetModelCarsWithPaginationQuery, PaginatedList<ModelCarDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public GetModelCarsWithPaginationQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PaginatedList<ModelCarDto>> Handle(GetModelCarsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ModelCars.AsNoTracking().AsQueryable();

            if (request.BrandId.HasValue)
                query = query.Where(c => c.BrandId == request.BrandId);

            return await query
                .ProjectTo<ModelCarDto>(_mapper.ConfigurationProvider)
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
