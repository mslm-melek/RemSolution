using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Payment.Commands.UploadPaymentProofCommand
{
    // Same file envelope as the client-document upload: a scan or a PDF, capped
    // so a mistyped upload cannot fill the disk.
    public class UploadPaymentProofCommandValidator : AbstractValidator<UploadPaymentProofCommand>
    {
        private const long MaxSizeBytes = 15 * 1024 * 1024;

        private static readonly string[] AllowedContentTypes = UploadFileTypes.DocumentContentTypes;

        private static readonly string[] AllowedExtensions = UploadFileTypes.DocumentExtensions;

        public UploadPaymentProofCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.PaymentId)
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
