using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.ReorderCarImagesCommand
{
    public class ReorderCarImagesCommandValidator : AbstractValidator<ReorderCarImagesCommand>
    {
        public ReorderCarImagesCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.CarId)
                .GreaterThan(0);

            RuleFor(c => c.OrderedImageIds)
                .NotEmpty()
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage(_ => localizer["Validation.CarImages.NoDuplicates"]);
        }
    }
}
