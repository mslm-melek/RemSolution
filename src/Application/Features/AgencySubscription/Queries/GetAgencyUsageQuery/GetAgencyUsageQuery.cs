using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.AgencySubscription.Queries.GetAgencyUsageQuery
{
    public class AgencyUsageDto
    {
        public bool HasSubscription { get; set; }
        public bool IsActive { get; set; }
        public string? PlanName { get; set; }
        public SubscriptionStatus? Status { get; set; }
        public int MaxCars { get; set; }
        public int MaxClients { get; set; }
        public int MaxUsers { get; set; }
        public int CarsUsed { get; set; }
        public int ClientsUsed { get; set; }
        public int UsersUsed { get; set; }
    }

    // Platform-admin view of one agency's quota usage vs. its plan limits.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record GetAgencyUsageQuery(int AgencyId) : IRequest<AgencyUsageDto>;

    public class GetAgencyUsageQueryHandler : IRequestHandler<GetAgencyUsageQuery, AgencyUsageDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;
        private readonly IIdentityService _identityService;

        public GetAgencyUsageQueryHandler(IApplicationDbContext context, TimeProvider dateTime, IIdentityService identityService)
        {
            _context = context;
            _dateTime = dateTime;
            _identityService = identityService;
        }

        public async Task<AgencyUsageDto> Handle(GetAgencyUsageQuery request, CancellationToken cancellationToken)
        {
            var now = _dateTime.GetUtcNow();

            // AgencySubscription is platform-level (not tenant-filtered), so the
            // explicit AgencyId filter is enough here.
            var subscription = await _context.AgencySubscriptions
                .AsNoTracking()
                .Where(s => s.AgencyId == request.AgencyId)
                .OrderByDescending(s => s.Status == SubscriptionStatus.Active && s.StartDate <= now && now < s.EndDate)
                .ThenByDescending(s => s.EndDate)
                .Select(s => new
                {
                    s.Plan!.Name,
                    s.Plan.MaxCars,
                    s.Plan.MaxClients,
                    s.Plan.MaxUsers,
                    s.Status,
                    IsActive = s.Status == SubscriptionStatus.Active && s.StartDate <= now && now < s.EndDate
                })
                .FirstOrDefaultAsync(cancellationToken);

            var usage = new AgencyUsageDto
            {
                HasSubscription = subscription is not null,
                IsActive = subscription?.IsActive ?? false,
                PlanName = subscription?.Name,
                Status = subscription?.Status,
                MaxCars = subscription?.MaxCars ?? 0,
                MaxClients = subscription?.MaxClients ?? 0,
                MaxUsers = subscription?.MaxUsers ?? 0,
            };

            // Cars/Clients are tenant-filtered: act as the agency for the counts.
            using (AmbientTenant.Push(request.AgencyId))
            {
                usage.CarsUsed = await _context.Cars.CountAsync(cancellationToken);
                usage.ClientsUsed = await _context.Clients.CountAsync(cancellationToken);
            }

            usage.UsersUsed = await _identityService.CountAgencyUsersAsync(request.AgencyId, cancellationToken);

            return usage;
        }
    }
}
