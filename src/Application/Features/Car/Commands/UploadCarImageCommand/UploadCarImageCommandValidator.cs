using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.UploadCarImageCommand
{
    public class UploadCarImageCommandValidator : AbstractValidator<UploadCarImageCommand>
    {
        public UploadCarImageCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.CarId)
                .GreaterThan(0);

            Include(FileUploadValidator.Image(localizer));
        }
    }
}
