using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyHomeWidgetsCommand
{
    // The caller picks which shortcut tiles their home screen shows, and in which
    // order. Separate from UpdateMyProfileCommand for the same reason the language
    // switch is: a home-screen tweak must not round-trip the display name and email.
    //
    // No permission check here beyond "signed in": the tiles the caller can
    // actually use are decided when the home screen renders them (feature enabled
    // AND read permission held, the same rule the navigation applies), and every
    // list behind a tile enforces its own permission. So a stale pin is a tile that
    // stops being drawn, never a way in.
    public record UpdateMyHomeWidgetsCommand : IRequest
    {
        // Ordered: the home screen draws them in the order given.
        public IReadOnlyCollection<string> Widgets { get; init; } = Array.Empty<string>();
    }

    public class UpdateMyHomeWidgetsCommandHandler : IRequestHandler<UpdateMyHomeWidgetsCommand>
    {
        private readonly IUser _user;
        private readonly IIdentityService _identityService;

        public UpdateMyHomeWidgetsCommandHandler(IUser user, IIdentityService identityService)
        {
            _user = user;
            _identityService = identityService;
        }

        public async Task Handle(UpdateMyHomeWidgetsCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var result = await _identityService.SetHomeWidgetsAsync(
                userId, request.Widgets, cancellationToken);

            if (!result.Succeeded)
            {
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.Widgets), e)));
            }
        }
    }
}

namespace RemSolution.Application.Features.Users.Commands.UpdateMyHomeWidgetsCommand
{
    public class UpdateMyHomeWidgetsCommandValidator : AbstractValidator<UpdateMyHomeWidgetsCommand>
    {
        public UpdateMyHomeWidgetsCommandValidator(ILocalizer localizer)
        {
            // An empty list is valid — it is how a user says "no tiles, thanks".
            RuleFor(v => v.Widgets)
                .NotNull()
                // Tiles only: the panel widgets render under the row rather than in
                // it, so they are not what the cap is protecting (see HomeWidgets).
                .Must(widgets => HomeWidgets.CountTiles(widgets) <= HomeWidgets.MaxPinned)
                .WithMessage(_ => localizer["Validation.HomeWidgets.TooMany", HomeWidgets.MaxPinned])
                .Must(widgets => widgets.Distinct().Count() == widgets.Count)
                .WithMessage(_ => localizer["Validation.HomeWidgets.Duplicate"])
                // Unknown keys are rejected rather than dropped: silently storing a
                // shorter list than was sent would leave the screen disagreeing with
                // what the user just saved.
                .Must(widgets => widgets.All(HomeWidgets.IsKnown))
                .WithMessage(_ => localizer["Validation.HomeWidgets.Unknown"]);
        }
    }
}
