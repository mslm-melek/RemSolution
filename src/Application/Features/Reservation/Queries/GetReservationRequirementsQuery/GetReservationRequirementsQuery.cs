using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Reservation.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Reservation.Queries.GetReservationRequirementsQuery
{
    /// <summary>What the agency asked this customer for, and where each ask stands.</summary>
    [Authorize(Policy = Permissions.ReservationRead)]
    [RequiresFeature(FeatureFlags.Reservations)]
    public record GetReservationRequirementsQuery(int ReservationId)
        : IRequest<IList<ReservationRequirementDto>>;

    public class GetReservationRequirementsQueryHandler
        : IRequestHandler<GetReservationRequirementsQuery, IList<ReservationRequirementDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetReservationRequirementsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ReservationRequirementDto>> Handle(
            GetReservationRequirementsQuery request, CancellationToken cancellationToken)
        {
            return await _context.ReservationRequirements
                .AsNoTracking()
                .Where(r => r.ReservationId == request.ReservationId)
                .OrderBy(r => r.Id)
                .ProjectToType<ReservationRequirementDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
