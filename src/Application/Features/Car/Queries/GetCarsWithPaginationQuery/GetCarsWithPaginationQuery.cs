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
        CarStatus? Status = null,
        // Cars with a hire running right now (or, false, everything else) — the
        // fleet figure the dashboard calls "on rent".
        bool? OnRent = null,
        // Half-open [AddedFrom, AddedTo) over when the car was recorded, which is
        // the only "added on" the model has.
        DateTimeOffset? AddedFrom = null,
        DateTimeOffset? AddedTo = null,
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

            if (request.Status.HasValue)
                query = query.Where(c => c.Status == request.Status);

            if (request.OnRent.HasValue)
                query = request.OnRent.Value
                    ? query.Where(c => c.Rentings!.Any(r => r.RentingState == RentingState.InProgress))
                    : query.Where(c => !c.Rentings!.Any(r => r.RentingState == RentingState.InProgress));

            if (request.AddedFrom.HasValue)
                query = query.Where(c => c.CreatedOn >= request.AddedFrom);

            if (request.AddedTo.HasValue)
                query = query.Where(c => c.CreatedOn < request.AddedTo);

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
                "rentings" => query.OrderByField(
                    c => c.Rentings!.Count(r => r.RentingState != RentingState.Cancelled), descending),
                _ => query.OrderByField(c => c.Matricule, descending),
            };

            return await ordered
                .ThenBy(c => c.Id)
                .ProjectToType<CarDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
