using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Users.Commands.SetAgencyUserActiveCommand
{
    // Platform admin deactivates (locks out indefinitely) or reactivates a user.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record SetAgencyUserActiveCommand : IRequest
    {
        public string UserId { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    public class SetAgencyUserActiveCommandHandler : IRequestHandler<SetAgencyUserActiveCommand>
    {
        private readonly IIdentityService _identityService;

        public SetAgencyUserActiveCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task Handle(SetAgencyUserActiveCommand request, CancellationToken cancellationToken)
        {
            var result = await _identityService.SetUserLockoutAsync(request.UserId, lockedOut: !request.IsActive, cancellationToken);

            if (!result.Succeeded)
            {
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.UserId), e)));
            }
        }
    }
}
