
namespace RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand
{
    public class CreateModelCarCommandValidator : AbstractValidator<CreateModelCarCommand>
    {
        public CreateModelCarCommandValidator()
        {
            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();
            RuleFor(x => x.BrandId)
                .NotNull().WithMessage("BrandId is required.");
        }
    }
}
