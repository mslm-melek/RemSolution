using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Reservation.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Reservation.Queries.GetReservationsWithPaginationQuery
{
    [Authorize(Policy = Permissions.ReservationRead)]
    [RequiresFeature(FeatureFlags.Reservations)]
    public record GetReservationsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? CarId = null,
        int? ClientId = null,
        ReservationStatus? Status = null,
        // Half-open [FromDate, ToDate) over the hold's START date, the same
        // convention the dashboard's period uses. A hold is "for" the day the car
        // goes out, which is the only one of its two dates the desk plans by — so
        // there is no DateBasis to choose here, unlike a renting's.
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        // Only holds still in play: awaiting the agency, confirmed, or paid. The
        // terminal ones (converted, rejected, cancelled, expired) are rows, but
        // they are not work — so the home screen's "today" queue can ask for the
        // ones that still need doing without naming a single status.
        bool ActiveOnly = false,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the latest start date first.
        string? SortBy = null,
        bool SortDescending = true
    ) : IRequest<PaginatedList<ReservationDto>>;

    public class GetReservationsWithPaginationQueryHandler
        : IRequestHandler<GetReservationsWithPaginationQuery, PaginatedList<ReservationDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetReservationsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<ReservationDto>> Handle(
            GetReservationsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Reservations.AsNoTracking().AsQueryable();

            if (request.CarId.HasValue)
                query = query.Where(r => r.CarId == request.CarId);

            if (request.ClientId.HasValue)
                query = query.Where(r => r.ClientId == request.ClientId);

            if (request.Status.HasValue)
                query = query.Where(r => r.Status == request.Status);

            // Both filters may be given at once: "today's holds that are still in
            // play" is one question, and Status pins it to one of the three.
            if (request.ActiveOnly)
                query = query.Where(r => r.Status == ReservationStatus.PendingConfirmation
                                         || r.Status == ReservationStatus.Confirmed
                                         || r.Status == ReservationStatus.Paid);

            var from = request.FromDate;
            var to = request.ToDate;

            if (from.HasValue)
                query = query.Where(r => r.StartDate >= from);

            if (to.HasValue)
                query = query.Where(r => r.StartDate < to);

            var descending = request.SortDescending;

            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "car" => query.OrderByField(r => r.Car!.Matricule, descending),
                "client" => query.OrderByField(r => r.Client!.LastName, descending)
                                 .ThenByField(r => r.Client!.FirstName, descending),
                "status" => query.OrderByField(r => r.Status, descending),
                "paid" => query.OrderByField(r => r.PayedPrice == null ? 0m : r.PayedPrice.Amount, descending),
                "price" => query.OrderByField(r => r.Price == null ? 0m : r.Price.Amount, descending),
                "expires" => query.OrderByField(r => r.ExpiresAt, descending),
                // "period" is one column showing both bounds; it sorts by the start.
                _ => query.OrderByField(r => r.StartDate, descending),
            };

            return await ordered
                .ThenBy(r => r.Id)
                .ProjectToType<ReservationDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
