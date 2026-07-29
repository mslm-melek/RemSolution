using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyHomeActionsCommand
{
    // The caller picks which quick actions their landing screen offers, and in
    // which order. Sibling of UpdateMyHomeWidgetsCommand — the figures and the
    // actions are chosen separately, so saving one must not overwrite the other.
    //
    // No permission check here beyond "signed in": the actions the caller can
    // actually use are decided when the screen renders them (role, feature enabled
    // AND create permission held, the same rule the navigation applies), and every
    // screen behind an action enforces its own permission. So a stale pick is an
    // action that stops being drawn, never a way in.
    public record UpdateMyHomeActionsCommand : IRequest
    {
        // Ordered: the screen draws them in the order given.
        public IReadOnlyCollection<string> Actions { get; init; } = Array.Empty<string>();
    }

    public class UpdateMyHomeActionsCommandHandler : IRequestHandler<UpdateMyHomeActionsCommand>
    {
        private readonly IUser _user;
        private readonly IIdentityService _identityService;

        public UpdateMyHomeActionsCommandHandler(IUser user, IIdentityService identityService)
        {
            _user = user;
            _identityService = identityService;
        }

        public async Task Handle(UpdateMyHomeActionsCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var result = await _identityService.SetHomeActionsAsync(
                userId, request.Actions, cancellationToken);

            if (!result.Succeeded)
            {
                throw new ValidationException(
                    result.Errors.Select(e => new ValidationFailure(nameof(request.Actions), e)));
            }
        }
    }
}

namespace RemSolution.Application.Features.Users.Commands.UpdateMyHomeActionsCommand
{
    public class UpdateMyHomeActionsCommandValidator : AbstractValidator<UpdateMyHomeActionsCommand>
    {
        public UpdateMyHomeActionsCommandValidator(ILocalizer localizer)
        {
            // An empty list is valid — it is how a user says "no actions, thanks".
            RuleFor(v => v.Actions)
                .NotNull()
                .Must(actions => actions.Count <= HomeActions.MaxPinned)
                .WithMessage(_ => localizer["Validation.HomeActions.TooMany", HomeActions.MaxPinned])
                .Must(actions => actions.Distinct().Count() == actions.Count)
                .WithMessage(_ => localizer["Validation.HomeActions.Duplicate"])
                // Unknown keys are rejected rather than dropped: silently storing a
                // shorter list than was sent would leave the screen disagreeing with
                // what the user just saved.
                .Must(actions => actions.All(HomeActions.IsKnown))
                .WithMessage(_ => localizer["Validation.HomeActions.Unknown"]);
        }
    }
}
