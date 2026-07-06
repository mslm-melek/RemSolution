using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencySubscription.Commands.AssignAgencySubscriptionCommand
{
    /// <summary>
    /// Platform-admin: subscribes an agency to a plan. Any currently Active
    /// subscription of the agency is marked Expired (superseded) — the database
    /// enforces at most one Active subscription per agency.
    /// </summary>
    public record AssignAgencySubscriptionCommand : IRequest<int>
    {
        public int AgencyId { get; init; }
        public int PlanId { get; init; }
        public DateTimeOffset StartDate { get; init; }
        public DateTimeOffset EndDate { get; init; }
    }

    public class AssignAgencySubscriptionCommandHandler : IRequestHandler<AssignAgencySubscriptionCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public AssignAgencySubscriptionCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(AssignAgencySubscriptionCommand request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .FindAsync(new object[] { request.AgencyId }, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, agency);

            var plan = await _context.SubscriptionPlans
                .FindAsync(new object[] { request.PlanId }, cancellationToken);

            Guard.Against.NotFound(request.PlanId, plan);

            var entity = new RemSolution.Domain.Entities.AgencySubscription
            {
                AgencyId = request.AgencyId,
                PlanId = request.PlanId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = SubscriptionStatus.Active
            };

            // Two saves in one transaction: the superseded rows must be expired
            // before the insert, or the one-Active-per-agency unique index
            // rejects the batch.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            var activeSubscriptions = await _context.AgencySubscriptions
                .Where(s => s.AgencyId == request.AgencyId && s.Status == SubscriptionStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var subscription in activeSubscriptions)
            {
                subscription.Status = SubscriptionStatus.Expired;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _context.AgencySubscriptions.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }
}
