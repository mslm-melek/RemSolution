using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Reservation.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationRequirementsQuery
{
    // The customer's own checklist for one of their reservations. Cross-tenant
    // read (lives under Features/MarketplaceSearch/, the sanctioned location);
    // the MarketplaceUserId link is the whole access rule, so somebody else's
    // reservation returns an empty list rather than a refusal.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyReservationRequirementsQuery(int ReservationId)
        : IRequest<IList<ReservationRequirementDto>>;

    public class GetMyReservationRequirementsQueryHandler
        : IRequestHandler<GetMyReservationRequirementsQuery, IList<ReservationRequirementDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public GetMyReservationRequirementsQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IList<ReservationRequirementDto>> Handle(
            GetMyReservationRequirementsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            return await _context.ReservationRequirements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.ReservationId == request.ReservationId
                            && r.Reservation != null
                            && r.Reservation.Client != null
                            && r.Reservation.Client.MarketplaceUserId == userId)
                .OrderBy(r => r.Id)
                .ProjectToType<ReservationRequirementDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
