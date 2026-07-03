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

        public GetModelCarsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<ModelCarDto>> Handle(GetModelCarsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ModelCars.AsNoTracking().AsQueryable();

            if (request.BrandId.HasValue)
                query = query.Where(c => c.BrandId == request.BrandId);

            return await query
                .ProjectToType<ModelCarDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
