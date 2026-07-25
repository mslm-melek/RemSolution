using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyProfileCommand
{
    // The caller edits their own display name and email. Changing the email
    // changes the login (see IIdentityService.UpdateProfileAsync).
    public record UpdateMyProfileCommand : IRequest
    {
        public string? FullName { get; init; }
        public string Email { get; init; } = string.Empty;
    }

    public class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand>
    {
        private readonly IUser _user;
        private readonly IIdentityService _identityService;

        public UpdateMyProfileCommandHandler(IUser user, IIdentityService identityService)
        {
            _user = user;
            _identityService = identityService;
        }

        public async Task Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var result = await _identityService.UpdateProfileAsync(
                userId, request.FullName, request.Email, cancellationToken);

            if (!result.Succeeded)
            {
                // Identity's verdict (e.g. email already taken) is user feedback.
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.Email), e)));
            }
        }
    }
}

namespace RemSolution.Application.Features.Users.Commands.UpdateMyProfileCommand
{
    public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
    {
        public UpdateMyProfileCommandValidator()
        {
            RuleFor(v => v.FullName).MaximumLength(200);
            RuleFor(v => v.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);
        }
    }
}
