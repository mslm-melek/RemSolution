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
        ReservationStatus? Status = null
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

            return await query
                .OrderByDescending(r => r.StartDate)
                .ProjectToType<ReservationDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
