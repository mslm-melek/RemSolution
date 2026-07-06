namespace RemSolution.Application.Features.Client.Commands.CreateClientCommand
{
    public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
    {
        public CreateClientCommandValidator()
        {
            RuleFor(c => c.FirstName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(c => c.LastName)
                .NotEmpty()
                .MaximumLength(100);
        }
    }
}
