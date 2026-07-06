namespace RemSolution.Application.Features.Car.Commands.CreateCarCommand
{
    public class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
    {
        public CreateCarCommandValidator()
        {
            RuleFor(v => v.Matricule)
                .MaximumLength(200)
                .NotEmpty();
            RuleFor(x => x.ModelId)
                .NotNull().WithMessage("ModelId is required.");
        }
    }
}
