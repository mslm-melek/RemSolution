using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.AgencySubscription.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.AgencySubscription.Queries.GetAgencySubscriptionsQuery
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record GetAgencySubscriptionsQuery : IRequest<IList<AgencySubscriptionDto>>
    {
        public int? AgencyId { get; init; }
    }

    public class GetAgencySubscriptionsQueryHandler : IRequestHandler<GetAgencySubscriptionsQuery, IList<AgencySubscriptionDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencySubscriptionsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<AgencySubscriptionDto>> Handle(GetAgencySubscriptionsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.AgencySubscriptions.AsNoTracking();

            if (request.AgencyId is int agencyId)
            {
                query = query.Where(s => s.AgencyId == agencyId);
            }

            return await query
                .OrderBy(s => s.AgencyId)
                .ThenByDescending(s => s.StartDate)
                .ProjectToType<AgencySubscriptionDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
