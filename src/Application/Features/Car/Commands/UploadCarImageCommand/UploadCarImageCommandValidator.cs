using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.UploadCarImageCommand
{
    public class UploadCarImageCommandValidator : AbstractValidator<UploadCarImageCommand>
    {
        private const long MaxSizeBytes = 15 * 1024 * 1024;

        private static readonly string[] AllowedContentTypes =
            { "image/jpeg", "image/png", "image/webp" };

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp" };

        public UploadCarImageCommandValidator(ILocalizer localizer)
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
