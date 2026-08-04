using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.CreateCarCommand
{
    public class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
    {
        public CreateCarCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Matricule)
                .MaximumLength(200)
                .NotEmpty();
            RuleFor(x => x.ModelId)
                .NotNull().WithMessage(_ => localizer["Validation.Car.ModelIdRequired"]);
            RuleFor(v => v.BranchId)
                .GreaterThan(0).When(v => v.BranchId.HasValue);
            RuleFor(v => v.Status)
                .IsInEnum();
            RuleFor(v => v.DailyRate)
                .GreaterThan(0).When(v => v.DailyRate.HasValue);
            // Zero is a real reading (a car delivered new), unlike a negative one.
            RuleFor(v => v.Mileage)
                .GreaterThanOrEqualTo(0).When(v => v.Mileage.HasValue);
        }
    }
}
