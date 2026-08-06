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
        // Free text over the plate and the model name — what somebody at the
        // counter has in front of them. Matches the client list's Search, so the
        // app bar's one box can hand the same term to either list.
        string? Search = null,
        int? ModelId = null,
        string? Color = null,
        FuelType? FuelType = null,
        CarStatus? Status = null,
        // Where the car is based, and who made it — the two ways a fleet is
        // narrowed down before anything else (see the list's filter rail). The
        // brand reads through the model, which is what carries it.
        int? BranchId = null,
        int? BrandId = null,
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
            // The same filters the counts beside the list are taken through, so a
            // rail reading "Maintenance 4" opens four rows (see CarQueryFilters).
            var query = _context.Cars
                .AsNoTracking()
                .ApplyCarFilters(
                    request.Search, request.ModelId, request.Color, request.FuelType, request.Status,
                    request.OnRent, request.BranchId, request.BrandId,
                    request.AddedFrom, request.AddedTo);

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
