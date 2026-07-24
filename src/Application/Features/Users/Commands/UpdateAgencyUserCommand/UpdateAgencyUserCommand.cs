using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Users.Commands.UpdateAgencyUserCommand
{
    // Platform admin edits an existing agency user's grants and/or role. The
    // Permissions array is a full replacement set. This touches only the
    // Identity/UserPermission rows (keyed by UserId, not tenant-filtered), so no
    // ambient tenant is needed.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record UpdateAgencyUserCommand : IRequest
    {
        public string UserId { get; init; } = string.Empty;
        // Null leaves the role unchanged.
        public string? Role { get; init; }
        // Full replacement set of permission grants (ignored for administrators).
        public string[] Permissions { get; init; } = Array.Empty<string>();
    }

    public class UpdateAgencyUserCommandHandler : IRequestHandler<UpdateAgencyUserCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;

        public UpdateAgencyUserCommandHandler(IApplicationDbContext context, IIdentityService identityService)
        {
            _context = context;
            _identityService = identityService;
        }

        public async Task Handle(UpdateAgencyUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _identityService.GetAgencyUserAsync(request.UserId, cancellationToken);

            if (user is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.UserId), $"User '{request.UserId}' was not found."),
                });
            }

            var effectiveRole = request.Role ?? user.Role;

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            // Replace the permission rows. Administrators hold every permission
            // implicitly, so they carry no rows.
            var existing = await _context.UserPermissions
                .Where(p => p.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            _context.UserPermissions.RemoveRange(existing);

            if (effectiveRole != Roles.AgencyAdministrator)
            {
                foreach (var permission in request.Permissions.Distinct())
                {
                    _context.UserPermissions.Add(new UserPermission
                    {
                        UserId = request.UserId,
                        Permission = permission,
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Role change (if any) + mandatory security-stamp refresh so the new
            // grants re-mint on the next request.
            var result = await _identityService.UpdateUserRoleAndStampAsync(request.UserId, request.Role, cancellationToken);

            if (!result.Succeeded)
            {
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.Role), e)));
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }
}
