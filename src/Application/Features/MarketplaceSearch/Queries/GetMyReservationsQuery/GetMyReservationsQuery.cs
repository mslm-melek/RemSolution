using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery
{
    // The signed-in customer's reservations across ALL agencies. Cross-tenant
    // read (lives under Features/MarketplaceSearch/, the sanctioned location).
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyReservationsQuery : IRequest<IList<MyReservationDto>>;

    public class GetMyReservationsQueryHandler : IRequestHandler<GetMyReservationsQuery, IList<MyReservationDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public GetMyReservationsQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IList<MyReservationDto>> Handle(GetMyReservationsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            return await _context.Reservations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Client != null && r.Client.MarketplaceUserId == userId)
                .OrderByDescending(r => r.StartDate)
                .ProjectToType<MyReservationDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
