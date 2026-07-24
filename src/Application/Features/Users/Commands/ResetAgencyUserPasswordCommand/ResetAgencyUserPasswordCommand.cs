using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Users.Commands.ResetAgencyUserPasswordCommand
{
    // Platform admin sets a new password for a user (no current-password check).
    // ISensitiveRequest: carries a password — never destructured into logs.
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record ResetAgencyUserPasswordCommand : IRequest, ISensitiveRequest
    {
        public string UserId { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }

    public class ResetAgencyUserPasswordCommandHandler : IRequestHandler<ResetAgencyUserPasswordCommand>
    {
        private readonly IIdentityService _identityService;

        public ResetAgencyUserPasswordCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task Handle(ResetAgencyUserPasswordCommand request, CancellationToken cancellationToken)
        {
            var result = await _identityService.AdminResetPasswordAsync(request.UserId, request.NewPassword, cancellationToken);

            if (!result.Succeeded)
            {
                // Identity's verdict (password policy) is user-input feedback.
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.NewPassword), e)));
            }
        }
    }
}
