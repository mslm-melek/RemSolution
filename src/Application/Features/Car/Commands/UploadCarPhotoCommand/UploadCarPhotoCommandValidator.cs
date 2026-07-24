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

        public UploadCarPhotoCommandValidator()
        {
            RuleFor(c => c.CarId)
                .GreaterThan(0);

            RuleFor(c => c.FileName)
                .NotEmpty()
                .Must(HaveAllowedExtension)
                .WithMessage($"File extension must be one of: {string.Join(", ", AllowedExtensions)}.");

            RuleFor(c => c.ContentType)
                .Must(ct => AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"Content type must be one of: {string.Join(", ", AllowedContentTypes)}.");

            RuleFor(c => c.Length)
                .GreaterThan(0).WithMessage("File is empty.")
                .LessThanOrEqualTo(MaxSizeBytes).WithMessage("File must not exceed 15 MB.");
        }

        private static bool HaveAllowedExtension(string fileName) =>
            AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }
}
