using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.SubscriptionPlan.DTOs;

namespace RemSolution.Application.Features.SubscriptionPlan.Queries.GetSubscriptionPlansQuery
{
    public record GetSubscriptionPlansQuery : IRequest<IList<SubscriptionPlanDto>>;

    public class GetSubscriptionPlansQueryHandler : IRequestHandler<GetSubscriptionPlansQuery, IList<SubscriptionPlanDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetSubscriptionPlansQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<SubscriptionPlanDto>> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
        {
            return await _context.SubscriptionPlans
                .AsNoTracking()
                .OrderBy(p => p.Price)
                .ProjectToType<SubscriptionPlanDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
