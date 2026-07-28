using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Expense.Commands.RecordExpensePaymentCommand
{
    // Settles part (or all) of an expense: moves PaidAmount by a delta rather
    // than taking an absolute total, so a caller working from a stale figure
    // still adds what it meant to add. The read-modify-write itself runs under
    // the per-agency write lock — without it two concurrent settlements would
    // both read the same starting total and the later write would swallow the
    // earlier one. Mirrors the client-side payment invariant: the settled total
    // may never exceed the expense amount, and a negative delta (a correction)
    // may not take it below zero.
    [Authorize(Policy = Permissions.ExpenseUpdate)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record RecordExpensePaymentCommand(int Id, decimal Amount) : IRequest;

    public class RecordExpensePaymentCommandHandler : IRequestHandler<RecordExpensePaymentCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public RecordExpensePaymentCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(RecordExpensePaymentCommand request, CancellationToken cancellationToken)
        {
            // Read and write inside the lock: reading first would reintroduce
            // the very race the lock is here to close.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            var entity = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            var total = entity.ExpenseAmount?.Amount ?? 0m;
            var settled = entity.PaidAmount?.Amount ?? 0m;
            var after = settled + request.Amount;

            if (after > total)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Amount),
                        $"This settlement would exceed the expense amount. Outstanding is {total - settled}.")
                });
            }

            if (after < 0m)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Amount),
                        $"A correction cannot take the settled total below zero (currently {settled}).")
                });
            }

            var currency = entity.ExpenseAmount?.Currency
                ?? (await _settings.GetAsync(entity.AgencyId, cancellationToken)).CurrencyCode;

            entity.PaidAmount = Money.Of(after, currency);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Expense.Commands.RecordExpensePaymentCommand
{
    public class RecordExpensePaymentCommandValidator : AbstractValidator<RecordExpensePaymentCommand>
    {
        public RecordExpensePaymentCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            // A delta: positive settles, negative corrects an over-recorded
            // settlement. Zero would be a no-op write.
            RuleFor(v => v.Amount).NotEqual(0m);
        }
    }
}
