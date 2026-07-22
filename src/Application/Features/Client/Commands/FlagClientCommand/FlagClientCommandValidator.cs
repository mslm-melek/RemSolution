namespace RemSolution.Application.Features.Client.Commands.FlagClientCommand
{
    public class FlagClientCommandValidator : AbstractValidator<FlagClientCommand>
    {
        public FlagClientCommandValidator()
        {
            RuleFor(c => c.Id)
                .GreaterThan(0);

            // Bound matches the Notes column length in ClientConfiguration.
            RuleFor(c => c.Notes)
                .MaximumLength(2000);

            // A flag with no explanation is not actionable for other staff; the
            // reason is required when raising it, optional when clearing.
            RuleFor(c => c.Notes)
                .NotEmpty().WithMessage("A reason (Notes) is required when flagging a client.")
                .When(c => c.IsFlagged);
        }
    }
}
