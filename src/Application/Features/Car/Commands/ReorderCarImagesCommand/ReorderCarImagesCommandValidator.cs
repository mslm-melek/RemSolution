namespace RemSolution.Application.Features.Car.Commands.ReorderCarImagesCommand
{
    public class ReorderCarImagesCommandValidator : AbstractValidator<ReorderCarImagesCommand>
    {
        public ReorderCarImagesCommandValidator()
        {
            RuleFor(c => c.CarId)
                .GreaterThan(0);

            RuleFor(c => c.OrderedImageIds)
                .NotEmpty()
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("OrderedImageIds must not contain duplicates.");
        }
    }
}
