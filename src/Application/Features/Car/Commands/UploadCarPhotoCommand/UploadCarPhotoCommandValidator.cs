using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.UploadCarPhotoCommand
{
    public class UploadCarPhotoCommandValidator : AbstractValidator<UploadCarPhotoCommand>
    {
        public UploadCarPhotoCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.CarId)
                .GreaterThan(0);

            Include(FileUploadValidator.Image(localizer));
        }
    }
}
