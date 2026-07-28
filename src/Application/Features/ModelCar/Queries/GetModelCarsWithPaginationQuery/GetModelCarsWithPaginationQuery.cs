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
        int? BrandId = null,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the model name.
        string? SortBy = null,
        bool SortDescending = false
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

            var descending = request.SortDescending;

            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "brand" => query.OrderByField(c => c.Brand!.Name, descending),
                _ => query.OrderByField(c => c.Name, descending),
            };

            return await ordered
                .ThenBy(c => c.Id)
                .ProjectToType<ModelCarDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
