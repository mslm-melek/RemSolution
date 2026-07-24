using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Users.Commands.SetMyAgencyUserActiveCommand
{
    // Agency administrator activates/deactivates a staff member of their OWN agency.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record SetMyAgencyUserActiveCommand : IRequest
    {
        public string UserId { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    public class SetMyAgencyUserActiveCommandHandler : IRequestHandler<SetMyAgencyUserActiveCommand>
    {
        private readonly IIdentityService _identityService;
        private readonly ITenantProvider _tenant;

        public SetMyAgencyUserActiveCommandHandler(IIdentityService identityService, ITenantProvider tenant)
        {
            _identityService = identityService;
            _tenant = tenant;
        }

        public async Task Handle(SetMyAgencyUserActiveCommand request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var targetAgencyId = await _identityService.GetUserAgencyIdAsync(request.UserId, cancellationToken);
            if (targetAgencyId != agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var result = await _identityService.SetUserLockoutAsync(request.UserId, lockedOut: !request.IsActive, cancellationToken);
            if (!result.Succeeded)
            {
                throw new ValidationException(result.Errors.Select(e => new ValidationFailure(nameof(request.UserId), e)));
            }
        }
    }
}
