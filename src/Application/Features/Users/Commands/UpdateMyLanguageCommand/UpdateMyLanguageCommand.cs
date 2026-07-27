using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyLanguageCommand
{
    // The caller picks their own UI language. Separate from UpdateMyProfileCommand
    // so the language switcher in the navigation bar does not have to round-trip
    // (and risk clobbering) the display name and email.
    public record UpdateMyLanguageCommand : IRequest
    {
        public string Language { get; init; } = string.Empty;
    }

    public class UpdateMyLanguageCommandHandler : IRequestHandler<UpdateMyLanguageCommand>
    {
        private readonly IUser _user;
        private readonly IIdentityService _identityService;

        public UpdateMyLanguageCommandHandler(IUser user, IIdentityService identityService)
        {
            _user = user;
            _identityService = identityService;
        }

        public async Task Handle(UpdateMyLanguageCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var result = await _identityService.SetPreferredLanguageAsync(
                userId, request.Language, cancellationToken);

            if (!result.Succeeded)
            {
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.Language), e)));
            }
        }
    }
}

namespace RemSolution.Application.Features.Users.Commands.UpdateMyLanguageCommand
{
    public class UpdateMyLanguageCommandValidator : AbstractValidator<UpdateMyLanguageCommand>
    {
        public UpdateMyLanguageCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Language)
                .NotEmpty()
                .Must(Languages.IsSupported)
                .WithMessage(_ => localizer["Validation.Language.Unsupported"]);
        }
    }
}
