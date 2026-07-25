using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Users.Commands.ChangeMyPasswordCommand
{
    // The caller changes their own password. ISensitiveRequest: never logged.
    public record ChangeMyPasswordCommand : IRequest, ISensitiveRequest
    {
        public string CurrentPassword { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }

    public class ChangeMyPasswordCommandHandler : IRequestHandler<ChangeMyPasswordCommand>
    {
        private readonly IUser _user;
        private readonly IIdentityService _identityService;

        public ChangeMyPasswordCommandHandler(IUser user, IIdentityService identityService)
        {
            _user = user;
            _identityService = identityService;
        }

        public async Task Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var result = await _identityService.ChangePasswordAsync(
                userId, request.CurrentPassword, request.NewPassword, cancellationToken);

            if (!result.Succeeded)
            {
                // Wrong current password or policy failure — both are user feedback.
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.CurrentPassword), e)));
            }
        }
    }
}

namespace RemSolution.Application.Features.Users.Commands.ChangeMyPasswordCommand
{
    public class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
    {
        public ChangeMyPasswordCommandValidator()
        {
            RuleFor(v => v.CurrentPassword).NotEmpty();
            // Complexity rules stay with Identity (the single source of truth);
            // this only rejects the obviously empty case before hitting it.
            RuleFor(v => v.NewPassword).NotEmpty();
        }
    }
}
