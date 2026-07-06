using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencySubscription.Commands.UpdateAgencySubscriptionCommand
{
    /// <summary>
    /// Platform-admin: manual status/period management (suspend, expire,
    /// reactivate, extend) — there is no billing provider integration yet.
    /// </summary>
    public record UpdateAgencySubscriptionCommand : IRequest
    {
        public int Id { get; init; }
        public SubscriptionStatus Status { get; init; }
        public DateTimeOffset EndDate { get; init; }
    }

    public class UpdateAgencySubscriptionCommandHandler : IRequestHandler<UpdateAgencySubscriptionCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateAgencySubscriptionCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateAgencySubscriptionCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.AgencySubscriptions
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Two saves in one transaction: when reactivating, any other Active
            // subscription must be expired first or the one-Active-per-agency
            // unique index rejects the change.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            if (request.Status == SubscriptionStatus.Active)
            {
                var otherActive = await _context.AgencySubscriptions
                    .Where(s => s.AgencyId == entity.AgencyId
                                && s.Id != entity.Id
                                && s.Status == SubscriptionStatus.Active)
                    .ToListAsync(cancellationToken);

                foreach (var subscription in otherActive)
                {
                    subscription.Status = SubscriptionStatus.Expired;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            entity.Status = request.Status;
            entity.EndDate = request.EndDate;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}
