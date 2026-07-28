using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;
using ExpenseEntity = RemSolution.Domain.Entities.Expense;

namespace RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand
{
    // Books a cost against one of the agency's cars (maintenance, fuel,
    // insurance…). The amount is what the agency owes; PaidAmount is how much of
    // it has already been settled — recording an expense that is paid on the spot
    // is the common case, so it is accepted here rather than forcing a second
    // call to RecordExpensePaymentCommand.
    //
    // AgencyId is not accepted from the client: TenantEntityInterceptor stamps it
    // from the current tenant on insert, and the tenant query filter is what
    // makes a car or type from another agency unreachable below.
    [Authorize(Policy = Permissions.ExpenseCreate)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record CreateExpenseCommand : IRequest<int>
    {
        public int CarId { get; init; }
        public int ExpenseTypeId { get; init; }
        // Defaults to now when omitted (stamped from TimeProvider, always UTC).
        public DateTime? ExpenseDate { get; init; }
        public decimal Amount { get; init; }
        // Already settled at booking time; defaults to nothing paid.
        public decimal PaidAmount { get; init; }
        public string? Description { get; init; }
    }

    public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public CreateExpenseCommandHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
        {
            // Tenant-filtered: a car belonging to another agency reads as absent.
            var car = await _context.Cars
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            var expenseType = await _context.ExpenseTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.ExpenseTypeId, cancellationToken);

            Guard.Against.NotFound(request.ExpenseTypeId, expenseType);

            if (!expenseType.IsActive)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.ExpenseTypeId),
                        "This expense type is no longer active.")
                });
            }

            if (request.PaidAmount > request.Amount)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.PaidAmount),
                        $"The settled amount cannot exceed the expense amount ({request.Amount}).")
                });
            }

            // Amounts are denominated in the agency's currency; the client sends
            // bare decimals. The tenant is always known here — the command is
            // agency-scoped and feature-gated.
            var agencyId = _tenant.AgencyId ?? car.AgencyId;
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            var entity = new ExpenseEntity
            {
                CarId = request.CarId,
                ExpenseTypeId = request.ExpenseTypeId,
                ExpenseDate = request.ExpenseDate ?? _dateTime.GetUtcNow().UtcDateTime,
                ExpenseAmount = Money.Of(request.Amount, currency),
                PaidAmount = Money.Of(request.PaidAmount, currency),
                Description = request.Description,
            };

            _context.Expenses.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand
{
    public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
    {
        public CreateExpenseCommandValidator()
        {
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.ExpenseTypeId).GreaterThan(0);
            RuleFor(v => v.Amount).GreaterThan(0);
            RuleFor(v => v.PaidAmount).GreaterThanOrEqualTo(0);
            RuleFor(v => v.Description).MaximumLength(1000);
        }
    }
}
