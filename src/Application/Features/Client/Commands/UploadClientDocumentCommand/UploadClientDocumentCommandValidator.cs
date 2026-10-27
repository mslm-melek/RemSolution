using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand
{
    public class UploadClientDocumentCommandValidator : AbstractValidator<UploadClientDocumentCommand>
    {
        public UploadClientDocumentCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.ClientId)
                .GreaterThan(0);

            RuleFor(c => c.DocumentType)
                .IsInEnum();

            RuleFor(c => c.FileName)
                .NotEmpty()
                .Must(HaveAllowedExtension)
                .WithMessage(_ => localizer["Validation.File.Extension", string.Join(", ", UploadFileTypes.DocumentExtensions)]);

            RuleFor(c => c.ContentType)
                .Must(ct => UploadFileTypes.DocumentContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
                .WithMessage(_ => localizer["Validation.File.ContentType", string.Join(", ", UploadFileTypes.DocumentContentTypes)]);

            RuleFor(c => c.Length)
                .GreaterThan(0).WithMessage(_ => localizer["Validation.File.Empty"])
                .LessThanOrEqualTo(UploadFileTypes.MaxSizeBytes).WithMessage(_ => localizer["Validation.File.TooLarge"]);
        }

        private static bool HaveAllowedExtension(string fileName) =>
            UploadFileTypes.DocumentExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }
}
