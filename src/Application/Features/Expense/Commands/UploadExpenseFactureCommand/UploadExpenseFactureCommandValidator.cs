using RemSolution.Application.Common.Files;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Expense.Commands.UploadExpenseFactureCommand
{
    public class UploadExpenseFactureCommandValidator : AbstractValidator<UploadExpenseFactureCommand>
    {
        public UploadExpenseFactureCommandValidator(ILocalizer localizer)
        {
            RuleFor(c => c.ExpenseId)
                .GreaterThan(0);

            Include(FileUploadValidator.Document(localizer));
        }
    }
}
