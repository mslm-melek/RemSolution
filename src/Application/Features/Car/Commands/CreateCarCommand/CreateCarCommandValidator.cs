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
            RuleFor(v => v.BranchId)
                .GreaterThan(0).When(v => v.BranchId.HasValue);
            RuleFor(v => v.Status)
                .IsInEnum();
            RuleFor(v => v.DailyRate)
                .GreaterThan(0).When(v => v.DailyRate.HasValue);
        }
    }
}
