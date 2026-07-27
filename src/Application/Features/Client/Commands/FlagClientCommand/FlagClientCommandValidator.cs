using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Client.Commands.FlagClientCommand
{
    public class FlagClientCommandValidator : AbstractValidator<FlagClientCommand>
    {
        public FlagClientCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.Id)
                .GreaterThan(0);

            // Bound matches the Notes column length in ClientConfiguration.
            RuleFor(c => c.Notes)
                .MaximumLength(2000);

            // A flag with no explanation is not actionable for other staff; the
            // reason is required when raising it, optional when clearing.
            RuleFor(c => c.Notes)
                .NotEmpty().WithMessage(_ => localizer["Validation.Client.FlagReasonRequired"])
                .When(c => c.IsFlagged);
        }
    }
}
