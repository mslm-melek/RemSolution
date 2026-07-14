using FluentValidation.Results;
using RemSolution.Application.Common.Exceptions;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.Features.Users.Commands.CreateAgencyUserCommand
{
    // The agency administrator creates a staff account in their own agency —
    // the tenant comes from the caller's claim, never from the request.
    // ISensitiveRequest: carries a password — never destructured into logs.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record CreateAgencyUserCommand : IRequest<string>, ISensitiveRequest
    {
        public string UserName { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        // Grants for the new staff user; effective from their first sign-in.
        public string[] Permissions { get; init; } = Array.Empty<string>();
    }

    public class CreateAgencyUserCommandHandler : IRequestHandler<CreateAgencyUserCommand, string>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CreateAgencyUserCommandHandler(
            IApplicationDbContext context,
            IIdentityService identityService,
            ITenantProvider tenant,
            TimeProvider dateTime)
        {
            _context = context;
            _identityService = identityService;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task<string> Handle(CreateAgencyUserCommand request, CancellationToken cancellationToken)
        {
            // The role check upstream implies a tenant; a missing claim means a
            // mis-provisioned admin, not a valid platform-wide caller.
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            // MaxUsers check and user insert are atomic under the per-agency
            // write lock — same discipline as the Car/Client quotas: a bare
            // count before an insert is a race two concurrent creates both
            // pass. UserManager writes through the same scoped DbContext, so
            // everything joins this transaction; disposing without commit
            // rolls back.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                _context, _tenant, _dateTime,
                (id, ct) => _identityService.CountAgencyUsersAsync(id, ct),
                p => p.MaxUsers, "users", cancellationToken);

            var (result, userId) = await _identityService.CreateAgencyUserAsync(
                request.UserName, request.Password, agencyId, cancellationToken);

            if (!result.Succeeded)
            {
                // Identity's verdict (duplicate name, password policy) is user
                // input feedback: surface it as a 400 validation problem.
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.UserName), e)));
            }

            foreach (var permission in request.Permissions.Distinct())
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    Permission = permission,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return userId;
        }
    }
}
