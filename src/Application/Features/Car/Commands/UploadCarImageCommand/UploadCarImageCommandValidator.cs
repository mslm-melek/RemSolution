namespace RemSolution.Application.Features.Car.Commands.UploadCarImageCommand
{
    public class UploadCarImageCommandValidator : AbstractValidator<UploadCarImageCommand>
    {
        private const long MaxSizeBytes = 5 * 1024 * 1024;

        private static readonly string[] AllowedContentTypes =
            { "image/jpeg", "image/png", "image/webp" };

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp" };

        public UploadCarImageCommandValidator()
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
                .LessThanOrEqualTo(MaxSizeBytes).WithMessage("File must not exceed 5 MB.");
        }

        private static bool HaveAllowedExtension(string fileName) =>
            AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }
}
