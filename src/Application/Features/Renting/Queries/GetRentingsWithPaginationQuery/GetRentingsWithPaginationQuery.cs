using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingsWithPaginationQuery
{
    // Which of a renting's two dates the [FromDate, ToDate) window is applied to.
    // "Running in July", "starting in July" and "ending in July" are three
    // different questions, and the dashboard asks all three.
    public enum RentingDateBasis
    {
        Overlaps = 0,
        Starts = 1,
        Ends = 2,
    }

    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        // Free text over the car's plate and the hirer's name — the two things
        // somebody at the counter can read off a key fob or a licence. Same
        // parameter as the car and client lists, so the app bar's one box hands
        // the same term to whichever list the user picked.
        string? Search = null,
        int? CarId = null,
        int? ClientId = null,
        RentingState? State = null,
        // Half-open [FromDate, ToDate), the same convention as the dashboard's
        // period — so a link from one of its counts selects the rows it counted.
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        RentingDateBasis DateBasis = RentingDateBasis.Overlaps,
        // A cancelled renting is still a row, but never part of "what happened":
        // the period counts leave it out, so a link from one of them can too.
        bool ExcludeCancelled = false,
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

            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(r =>
                    (r.Car != null && r.Car.Matricule != null && r.Car.Matricule.Contains(request.Search)) ||
                    (r.Client != null && r.Client.FirstName != null && r.Client.FirstName.Contains(request.Search)) ||
                    (r.Client != null && r.Client.LastName != null && r.Client.LastName.Contains(request.Search)));

            if (request.CarId.HasValue)
                query = query.Where(r => r.CarId == request.CarId);

            if (request.ClientId.HasValue)
                query = query.Where(r => r.ClientId == request.ClientId || r.SecondClientId == request.ClientId);

            if (request.State.HasValue)
                query = query.Where(r => r.RentingState == request.State);

            if (request.ExcludeCancelled)
                query = query.Where(r => r.RentingState != RentingState.Cancelled);

            var from = request.FromDate;
            var to = request.ToDate;

            switch (request.DateBasis)
            {
                case RentingDateBasis.Starts:
                    if (from.HasValue) query = query.Where(r => r.StartDate >= from);
                    if (to.HasValue) query = query.Where(r => r.StartDate < to);
                    break;

                case RentingDateBasis.Ends:
                    if (from.HasValue) query = query.Where(r => r.EndDate >= from);
                    if (to.HasValue) query = query.Where(r => r.EndDate < to);
                    break;

                // Overlaps: everything running at some point inside the window.
                default:
                    if (from.HasValue) query = query.Where(r => r.EndDate >= from);
                    if (to.HasValue) query = query.Where(r => r.StartDate < to);
                    break;
            }

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
