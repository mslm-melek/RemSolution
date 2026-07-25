using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Reservation.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Reservation.Queries.GetReservationByIdQuery
{
    [Authorize(Policy = Permissions.ReservationRead)]
    [RequiresFeature(FeatureFlags.Reservations)]
    public record GetReservationByIdQuery(int Id) : IRequest<ReservationDto?>;

    public class GetReservationByIdQueryHandler : IRequestHandler<GetReservationByIdQuery, ReservationDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetReservationByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReservationDto?> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .Where(r => r.Id == request.Id)
                .ProjectToType<ReservationDto>()
                .FirstOrDefaultAsync(cancellationToken);

            if (reservation == null)
                throw new NotFoundException("Reservation", request.Id.ToString());

            return reservation;
        }
    }
}
