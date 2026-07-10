using FluentValidation.Results;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.DeleteSubscriptionPlanCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("DeleteSubscriptionPlan", "SubscriptionPlan")]
    public record DeleteSubscriptionPlanCommand(int Id) : IRequest;

    public class DeleteSubscriptionPlanCommandHandler : IRequestHandler<DeleteSubscriptionPlanCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteSubscriptionPlanCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteSubscriptionPlanCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SubscriptionPlans
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // The Plan FK is Restrict: deleting a referenced plan would surface
            // as a raw DbUpdateException (500) — turn it into a 400 instead.
            var isReferenced = await _context.AgencySubscriptions
                .AnyAsync(s => s.PlanId == request.Id, cancellationToken);

            if (isReferenced)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "The plan is referenced by agency subscriptions and cannot be deleted.")
                });
            }

            _context.SubscriptionPlans.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
