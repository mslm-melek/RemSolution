
namespace RemSolution.Application.Features.ModelCar.Commands.UpdateModelCarCommand
{
    public class UpdateModelCarCommandValidator : AbstractValidator<UpdateModelCarCommand>
    {
        public UpdateModelCarCommandValidator()
        {
            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();
            RuleFor(x => x.BrandId)
                .NotNull().WithMessage("BrandId is required.");
        }
    }
}
