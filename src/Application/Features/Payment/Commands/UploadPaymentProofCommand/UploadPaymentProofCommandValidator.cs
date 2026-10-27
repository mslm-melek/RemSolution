using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Payment.Commands.UploadPaymentProofCommand
{
    public class UploadPaymentProofCommandValidator : AbstractValidator<UploadPaymentProofCommand>
    {
        public UploadPaymentProofCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.PaymentId)
                .GreaterThan(0);

            Include(FileUploadValidator.Document(localizer));
        }
    }
}
