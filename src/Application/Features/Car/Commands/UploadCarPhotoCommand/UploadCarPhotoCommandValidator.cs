using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.UploadCarPhotoCommand
{
    public class UploadCarPhotoCommandValidator : AbstractValidator<UploadCarPhotoCommand>
    {
        private const long MaxSizeBytes = 15 * 1024 * 1024;

        // A car photo is an image only — no PDF, unlike identity documents.
        private static readonly string[] AllowedContentTypes =
            { "image/jpeg", "image/png", "image/webp" };

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp" };

        public UploadCarPhotoCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.CarId)
                .GreaterThan(0);

            RuleFor(c => c.FileName)
                .NotEmpty()
                .Must(HaveAllowedExtension)
                .WithMessage(_ => localizer["Validation.File.Extension", string.Join(", ", AllowedExtensions)]);

            RuleFor(c => c.ContentType)
                .Must(ct => AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
                .WithMessage(_ => localizer["Validation.File.ContentType", string.Join(", ", AllowedContentTypes)]);

            RuleFor(c => c.Length)
                .GreaterThan(0).WithMessage(_ => localizer["Validation.File.Empty"])
                .LessThanOrEqualTo(MaxSizeBytes).WithMessage(_ => localizer["Validation.File.TooLarge"]);
        }

        private static bool HaveAllowedExtension(string fileName) =>
            AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }
}
