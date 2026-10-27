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

            Include(FileUploadValidator.Document(localizer));
        }
    }
}
