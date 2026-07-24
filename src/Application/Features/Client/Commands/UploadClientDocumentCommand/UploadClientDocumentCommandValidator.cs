namespace RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand
{
    public class UploadClientDocumentCommandValidator : AbstractValidator<UploadClientDocumentCommand>
    {
        private const long MaxSizeBytes = 15 * 1024 * 1024;

        private static readonly string[] AllowedContentTypes =
            { "image/jpeg", "image/png", "image/webp", "application/pdf" };

        private static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

        public UploadClientDocumentCommandValidator()
        {
            RuleFor(c => c.ClientId)
                .GreaterThan(0);

            RuleFor(c => c.DocumentType)
                .IsInEnum();

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
