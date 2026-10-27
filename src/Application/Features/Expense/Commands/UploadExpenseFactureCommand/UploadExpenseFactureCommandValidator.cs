using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Expense.Commands.UploadExpenseFactureCommand
{
    // Same file envelope as the client-document upload: a scan or a PDF, capped
    // so a mistyped upload cannot fill the disk.
    public class UploadExpenseFactureCommandValidator : AbstractValidator<UploadExpenseFactureCommand>
    {
        public UploadExpenseFactureCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.ExpenseId)
                .GreaterThan(0);

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
