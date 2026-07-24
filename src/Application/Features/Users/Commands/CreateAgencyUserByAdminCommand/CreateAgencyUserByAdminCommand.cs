using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Users.Commands.CreateAgencyUserByAdminCommand
{
    // The platform administrator provisions a user (administrator or staff) for
    // a specified agency — the counterpart to CreateAgencyUserCommand, which is
    // the agency admin creating their own staff. The target agency comes from
    // the request (validated), and the tenant context for the quota check is
    // established with AmbientTenant.Push.
    // ISensitiveRequest: carries a password — never destructured into logs.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record CreateAgencyUserByAdminCommand : IRequest<string>, ISensitiveRequest
    {
        public int AgencyId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        // AgencyAdministrator or AgencyStaff.
        public string Role { get; init; } = Roles.AgencyStaff;
        // Grants for a staff user; ignored for an administrator (admins hold all).
        public string[] Permissions { get; init; } = Array.Empty<string>();
    }

    public class CreateAgencyUserByAdminCommandHandler : IRequestHandler<CreateAgencyUserByAdminCommand, string>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CreateAgencyUserByAdminCommandHandler(
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

        public async Task<string> Handle(CreateAgencyUserByAdminCommand request, CancellationToken cancellationToken)
        {
            if (!await _context.Agencies.AnyAsync(a => a.Id == request.AgencyId, cancellationToken))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.AgencyId), $"Agency '{request.AgencyId}' was not found."),
                });
            }

            // Act as the target agency for the duration: the write lock, the plan
            // lookup and the MaxUsers count all read ITenantProvider.AgencyId, so
            // pushing the ambient scopes them to this agency with no other changes.
            using var _ = AmbientTenant.Push(request.AgencyId);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                _context, _tenant, _dateTime,
                (id, ct) => _identityService.CountAgencyUsersAsync(id, ct),
                p => p.MaxUsers, "users", cancellationToken);

            var (result, userId) = await _identityService.CreateAgencyUserAsync(
                request.UserName, request.Password, request.AgencyId, request.Role, cancellationToken);

            if (!result.Succeeded)
            {
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.UserName), e)));
            }

            // Administrators hold every permission implicitly; only persist grants
            // for staff.
            if (request.Role != Roles.AgencyAdministrator)
            {
                foreach (var permission in request.Permissions.Distinct())
                {
                    _context.UserPermissions.Add(new UserPermission
                    {
                        UserId = userId,
                        Permission = permission,
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return userId;
        }
    }
}
