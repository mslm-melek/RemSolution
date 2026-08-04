using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Expense.Commands.UpdateExpenseCommand
{
    // Corrects a booked expense. The settled total is moved by
    // RecordExpensePaymentCommand, not here, so an edit of the amount can never
    // silently wipe a settlement — but lowering the amount below what is already
    // settled is refused for the same reason.
    [Authorize(Policy = Permissions.ExpenseUpdate)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record UpdateExpenseCommand : IRequest
    {
        public int Id { get; init; }
        public int CarId { get; init; }
        public int ExpenseTypeId { get; init; }
        public DateTime ExpenseDate { get; init; }
        public decimal Amount { get; init; }
        /// <summary>
        /// The odometer when the cost was incurred (see the create command). An
        /// edit may correct it in either direction — unlike a new reading, which
        /// only moves the car's odometer forward — because the correction being
        /// made is often that the figure was wrong.
        /// </summary>
        public int? Mileage { get; init; }
        public string? Description { get; init; }
    }

    public class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public UpdateExpenseCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Tenant-filtered reads: a car or type from another agency is absent.
            var car = await _context.Cars
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            var expenseType = await _context.ExpenseTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.ExpenseTypeId, cancellationToken);

            Guard.Against.NotFound(request.ExpenseTypeId, expenseType);

            var settled = entity.PaidAmount?.Amount ?? 0m;

            if (request.Amount < settled)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Amount),
                        $"The amount cannot be lower than what is already settled ({settled}).")
                });
            }

            var currency = (await _settings.GetAsync(entity.AgencyId, cancellationToken)).CurrencyCode;

            entity.CarId = request.CarId;
            entity.ExpenseTypeId = request.ExpenseTypeId;
            entity.ExpenseDate = request.ExpenseDate;
            entity.ExpenseAmount = Money.Of(request.Amount, currency);
            entity.Mileage = request.Mileage;
            entity.Description = request.Description;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Expense.Commands.UpdateExpenseCommand
{
    public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
    {
        public UpdateExpenseCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.ExpenseTypeId).GreaterThan(0);
            RuleFor(v => v.ExpenseDate).NotEmpty();
            RuleFor(v => v.Amount).GreaterThan(0);
            RuleFor(v => v.Mileage).GreaterThanOrEqualTo(0).When(v => v.Mileage.HasValue);
            RuleFor(v => v.Description).MaximumLength(1000);
        }
    }
}
