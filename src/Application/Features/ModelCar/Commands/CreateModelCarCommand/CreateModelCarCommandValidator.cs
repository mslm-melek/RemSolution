
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand
{
    public class CreateModelCarCommandValidator : AbstractValidator<CreateModelCarCommand>
    {
        public CreateModelCarCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();
            RuleFor(x => x.BrandId)
                .NotNull().WithMessage(_ => localizer["Validation.ModelCar.BrandIdRequired"]);
        }
    }
}
