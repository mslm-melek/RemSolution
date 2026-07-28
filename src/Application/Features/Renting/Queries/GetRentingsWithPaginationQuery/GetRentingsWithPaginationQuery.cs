using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingsWithPaginationQuery
{
    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? CarId = null,
        int? ClientId = null,
        RentingState? State = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the latest start date first.
        string? SortBy = null,
        bool SortDescending = true
    ) : IRequest<PaginatedList<RentingDto>>;

    public class GetRentingsWithPaginationQueryHandler
        : IRequestHandler<GetRentingsWithPaginationQuery, PaginatedList<RentingDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetRentingsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<RentingDto>> Handle(
            GetRentingsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Rentings.AsNoTracking().AsQueryable();

            if (request.CarId.HasValue)
                query = query.Where(r => r.CarId == request.CarId);

            if (request.ClientId.HasValue)
                query = query.Where(r => r.ClientId == request.ClientId || r.SecondClientId == request.ClientId);

            if (request.State.HasValue)
                query = query.Where(r => r.RentingState == request.State);

            if (request.FromDate.HasValue)
                query = query.Where(r => r.EndDate >= request.FromDate);

            if (request.ToDate.HasValue)
                query = query.Where(r => r.StartDate <= request.ToDate);

            var descending = request.SortDescending;

            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "car" => query.OrderByField(r => r.Car!.Matricule, descending),
                "client" => query.OrderByField(r => r.Client!.LastName, descending)
                                 .ThenByField(r => r.Client!.FirstName, descending),
                "state" => query.OrderByField(r => r.RentingState, descending),
                "price" => query.OrderByField(r => r.Price == null ? 0m : r.Price.Amount, descending),
                "enddate" => query.OrderByField(r => r.EndDate, descending),
                // "period" is one column showing both bounds; it sorts by the start.
                _ => query.OrderByField(r => r.StartDate, descending),
            };

            return await ordered
                .ThenBy(r => r.Id)
                .ProjectToType<RentingDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
