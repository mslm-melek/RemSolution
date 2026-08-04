namespace RemSolution.Application.Features.Car.Commands.UpdateCarCommand
{
    public class UpdateCarCommandValidator : AbstractValidator<UpdateCarCommand>
    {
        public UpdateCarCommandValidator()
        {
            RuleFor(v => v.Id)
                .GreaterThan(0);
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
