using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencySubscription.Queries.GetMySubscriptionQuery
{
    public class MySubscriptionDto
    {
        public string PlanName { get; init; } = string.Empty;
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public int MaxUsers { get; init; }
        public decimal Price { get; init; }
        public DateTimeOffset StartDate { get; init; }
        public DateTimeOffset EndDate { get; init; }
        public SubscriptionStatus Status { get; init; }
        public bool IsActive { get; init; }
        public int CarsUsed { get; set; }
        public int ClientsUsed { get; set; }
        public int UsersUsed { get; set; }
    }

    /// <summary>
    /// The calling agency's subscription (active one if any, otherwise the most
    /// recent) plus current quota usage. Null when the caller has no tenant or
    /// the agency was never subscribed.
    /// </summary>
    public record GetMySubscriptionQuery : IRequest<MySubscriptionDto?>;

    public class GetMySubscriptionQueryHandler : IRequestHandler<GetMySubscriptionQuery, MySubscriptionDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;
        private readonly IIdentityService _identityService;

        public GetMySubscriptionQueryHandler(IApplicationDbContext context, ITenantProvider tenant, TimeProvider dateTime, IIdentityService identityService)
        {
            _context = context;
            _tenant = tenant;
            _dateTime = dateTime;
            _identityService = identityService;
        }

        public async Task<MySubscriptionDto?> Handle(GetMySubscriptionQuery request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                return null;
            }

            var now = _dateTime.GetUtcNow();

            var subscription = await _context.AgencySubscriptions
                .AsNoTracking()
                .Where(s => s.AgencyId == agencyId)
                .OrderByDescending(s => s.Status == SubscriptionStatus.Active && s.StartDate <= now && now < s.EndDate)
                .ThenByDescending(s => s.EndDate)
                .Select(s => new MySubscriptionDto
                {
                    PlanName = s.Plan!.Name,
                    MaxCars = s.Plan.MaxCars,
                    MaxClients = s.Plan.MaxClients,
                    MaxUsers = s.Plan.MaxUsers,
                    Price = s.Plan.Price,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = s.Status,
                    IsActive = s.Status == SubscriptionStatus.Active && s.StartDate <= now && now < s.EndDate
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is null)
            {
                return null;
            }

            // Tenant query filters scope both counts to the calling agency.
            subscription.CarsUsed = await _context.Cars.CountAsync(cancellationToken);
            subscription.ClientsUsed = await _context.Clients.CountAsync(cancellationToken);
            // Users live in the Identity store (no tenant filter): counted via
            // the same service the MaxUsers quota check uses.
            subscription.UsersUsed = await _identityService.CountAgencyUsersAsync(agencyId, cancellationToken);

            return subscription;
        }
    }
}
