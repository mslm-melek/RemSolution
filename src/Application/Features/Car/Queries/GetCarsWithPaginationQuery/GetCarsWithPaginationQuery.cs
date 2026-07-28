using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery
{
    [Authorize(Policy = Permissions.CarRead)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record GetCarsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? ModelId = null,
        string? Color = null,
        FuelType? FuelType = null,
        // Column the table is sorted by, named after the Angular matColumnDef.
        // Anything unrecognised falls back to the matricule (see SortingExtensions).
        string? SortBy = null,
        bool SortDescending = false
    ) : IRequest<PaginatedList<CarDto>>;
    public class GetCarsWithPaginationQueryHandler
        : IRequestHandler<GetCarsWithPaginationQuery, PaginatedList<CarDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetCarsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<CarDto>> Handle(GetCarsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Cars.AsNoTracking().AsQueryable();

            if (request.ModelId.HasValue)
                query = query.Where(c => c.ModelId == request.ModelId);

            if (!string.IsNullOrWhiteSpace(request.Color))
                query = query.Where(c => c.Color == request.Color);

            if (request.FuelType.HasValue)
                query = query.Where(c => c.FuelType == request.FuelType);

            var descending = request.SortDescending;

            // Ordered before the projection so the sort runs on indexed entity
            // columns; Id is the tie-break that keeps paging stable.
            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "model" => query.OrderByField(c => c.Model!.Name, descending),
                "firstcirculationdate" => query.OrderByField(c => c.FirstCirculationDate, descending),
                "color" => query.OrderByField(c => c.Color, descending),
                "power" => query.OrderByField(c => c.Power, descending),
                "fueltype" => query.OrderByField(c => c.FuelType, descending),
                "status" => query.OrderByField(c => c.Status, descending),
                "dailyrate" => query.OrderByField(c => c.DailyRate == null ? 0m : c.DailyRate.Amount, descending),
                "branch" => query.OrderByField(c => c.Branch!.Name, descending),
                _ => query.OrderByField(c => c.Matricule, descending),
            };

            return await ordered
                .ThenBy(c => c.Id)
                .ProjectToType<CarDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
