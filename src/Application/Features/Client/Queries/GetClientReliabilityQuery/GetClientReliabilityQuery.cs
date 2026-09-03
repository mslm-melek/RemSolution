using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Client.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Client.Queries.GetClientReliabilityQuery
{
    /// <summary>
    /// A client's record with this agency, computed rather than stored: it is a
    /// summary of rows that already exist, and a stored counter would only be a
    /// second version of the truth to keep in step.
    /// <para>
    /// Read on Reservation.Read rather than Client.Read: it is shown at the moment
    /// a hold is confirmed, and a counter that can answer a booking request has to
    /// be able to see what it is answering.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.ReservationRead)]
    [RequiresFeature(FeatureFlags.Reservations)]
    public record GetClientReliabilityQuery(int ClientId) : IRequest<ClientReliabilityDto>;

    public class GetClientReliabilityQueryHandler
        : IRequestHandler<GetClientReliabilityQuery, ClientReliabilityDto>
    {
        // What one cancellation costs, and what being late costs on top.
        private const int CancellationPenalty = 15;
        private const int LatePenalty = 15;

        private readonly IApplicationDbContext _context;

        public GetClientReliabilityQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ClientReliabilityDto> Handle(
            GetClientReliabilityQuery request, CancellationToken cancellationToken)
        {
            // Tenant-filtered on both sets, so this only ever counts what happened
            // with THIS agency. Read as rows and counted here rather than
            // aggregated in SQL: the fee is an owned Money, which EF cannot reach
            // inside a Count(...), and one client's bookings are tens of rows.
            var holds = await _context.Reservations
                .AsNoTracking()
                .Where(r => r.ClientId == request.ClientId)
                .Select(r => new
                {
                    r.Status,
                    r.CancelledByCustomer,
                    Fee = (decimal?)r.CancellationFee!.Amount,
                })
                .ToListAsync(cancellationToken);

            var hires = await _context.Rentings
                .AsNoTracking()
                .Where(r => r.ClientId == request.ClientId)
                .Select(r => r.RentingState)
                .ToListAsync(cancellationToken);

            // Only what the customer called off themselves. A hold the agency
            // cancelled, refused, or let lapse is not their doing.
            var theirCancellations = holds
                .Where(h => h.Status == ReservationStatus.Cancelled && h.CancelledByCustomer)
                .ToList();

            var cancellations = theirCancellations.Count;
            var late = theirCancellations.Count(h => h.Fee > 0m);

            // A converted hold IS the hire it became, so counting both would
            // double it: the hires are the bookings that ran.
            var bookings =
                holds.Count(h => h.Status != ReservationStatus.Converted) + hires.Count;

            var score = Math.Clamp(
                100 - (cancellations * CancellationPenalty) - (late * LatePenalty), 0, 100);

            return new ClientReliabilityDto
            {
                ClientId = request.ClientId,
                Bookings = bookings,
                Cancellations = cancellations,
                LateCancellations = late,
                Completed = hires.Count(state => state == RentingState.Done),
                Score = score,
            };
        }
    }
}
