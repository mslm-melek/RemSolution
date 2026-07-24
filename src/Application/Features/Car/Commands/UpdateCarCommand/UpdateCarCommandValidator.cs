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
        }
    }
}
