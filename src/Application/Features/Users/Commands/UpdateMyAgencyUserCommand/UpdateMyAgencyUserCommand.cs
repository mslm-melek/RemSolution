using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyAgencyUserCommand
{
    // Agency administrator sets a staff member's permissions within their OWN
    // agency. The target must belong to the caller's agency; grants are limited
    // to permissions whose feature is enabled for the agency.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record UpdateMyAgencyUserCommand : IRequest
    {
        public string UserId { get; init; } = string.Empty;
        // Full replacement set of permission grants.
        public string[] Permissions { get; init; } = Array.Empty<string>();
    }

    public class UpdateMyAgencyUserCommandHandler : IRequestHandler<UpdateMyAgencyUserCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IIdentityService _identityService;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public UpdateMyAgencyUserCommandHandler(
            IApplicationDbContext context, IIdentityService identityService, ITenantProvider tenant, TimeProvider dateTime)
        {
            _context = context;
            _identityService = identityService;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task Handle(UpdateMyAgencyUserCommand request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            // The target must be a user of the caller's own agency.
            var targetAgencyId = await _identityService.GetUserAgencyIdAsync(request.UserId, cancellationToken);
            if (targetAgencyId != agencyId)
            {
                throw new ForbiddenAccessException();
            }

            // A grant only counts while its feature is enabled — drop the rest so
            // the admin cannot grant a permission for a module their plan excludes.
            var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
                _context, agencyId, _dateTime.GetUtcNow(), cancellationToken);
            var permissions = FeatureCatalog.EffectivePermissions(request.Permissions.Distinct(), enabled).ToList();

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            var existing = await _context.UserPermissions
                .Where(p => p.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            _context.UserPermissions.RemoveRange(existing);

            foreach (var permission in permissions)
            {
                _context.UserPermissions.Add(new UserPermission { UserId = request.UserId, Permission = permission });
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Refresh the security stamp so the new grants re-mint on next request.
            var result = await _identityService.UpdateUserRoleAndStampAsync(request.UserId, role: null, cancellationToken);
            if (!result.Succeeded)
            {
                throw new ValidationException(result.Errors.Select(e => new ValidationFailure(nameof(request.UserId), e)));
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }
}
