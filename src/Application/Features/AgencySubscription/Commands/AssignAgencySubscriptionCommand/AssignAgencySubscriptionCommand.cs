using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencySubscription.Commands.AssignAgencySubscriptionCommand
{
    /// <summary>
    /// The outcome of assigning a plan. When the agency had no users yet, an
    /// AgencyAdministrator is bootstrapped and its one-time credentials are
    /// returned so the platform admin can hand them over (the admin resets the
    /// password on first sign-in).
    /// </summary>
    public record AssignAgencySubscriptionResult(
        int SubscriptionId,
        string? AdminUserName,
        string? AdminTemporaryPassword);

    /// <summary>
    /// Platform-admin: subscribes an agency to a plan. Any currently Active
    /// subscription of the agency is marked Expired (superseded) — the database
    /// enforces at most one Active subscription per agency. If the agency has no
    /// users yet, an administrator account is created automatically (requires the
    /// agency to have an email, used as the username).
    /// </summary>
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("AssignAgencySubscription", "AgencySubscription")]
    public record AssignAgencySubscriptionCommand : IRequest<AssignAgencySubscriptionResult>
    {
        public int AgencyId { get; init; }
        public int PlanId { get; init; }
        public DateTimeOffset StartDate { get; init; }
        public DateTimeOffset EndDate { get; init; }
    }

    public class AssignAgencySubscriptionCommandHandler : IRequestHandler<AssignAgencySubscriptionCommand, AssignAgencySubscriptionResult>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;

        public AssignAgencySubscriptionCommandHandler(IApplicationDbContext context, IIdentityService identityService)
        {
            _context = context;
            _identityService = identityService;
        }

        public async Task<AssignAgencySubscriptionResult> Handle(AssignAgencySubscriptionCommand request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .FindAsync(new object[] { request.AgencyId }, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, agency);

            var plan = await _context.SubscriptionPlans
                .FindAsync(new object[] { request.PlanId }, cancellationToken);

            Guard.Against.NotFound(request.PlanId, plan);

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

            var entity = new RemSolution.Domain.Entities.AgencySubscription
            {
                AgencyId = request.AgencyId,
                PlanId = request.PlanId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = SubscriptionStatus.Active
            };

            _context.AgencySubscriptions.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            // Bootstrap the agency's first administrator when it has no users yet.
            string? adminUserName = null;
            string? adminPassword = null;

            var userCount = await _identityService.CountAgencyUsersAsync(request.AgencyId, cancellationToken);

            if (userCount == 0)
            {
                if (string.IsNullOrWhiteSpace(agency!.Email))
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.AgencyId),
                            "The agency has no users and no email: set an agency email before assigning a plan, so the first administrator account can be created."),
                    });
                }

                adminUserName = agency.Email;
                adminPassword = GenerateTemporaryPassword();

                // AgencyAdministrator holds every permission implicitly — no
                // UserPermission rows needed.
                var (result, _) = await _identityService.CreateAgencyUserAsync(
                    adminUserName, adminPassword, request.AgencyId, Roles.AgencyAdministrator, cancellationToken);

                if (!result.Succeeded)
                {
                    throw new ValidationException(
                        result.Errors.Select(e => new ValidationFailure(nameof(request.AgencyId), e)));
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return new AssignAgencySubscriptionResult(entity.Id, adminUserName, adminPassword);
        }

        // A temporary password that satisfies the default Identity complexity
        // rules (upper, lower, digit, symbol, length). The admin resets it.
        private static string GenerateTemporaryPassword() =>
            "Aa1!" + Guid.NewGuid().ToString("N")[..12];
    }
}
