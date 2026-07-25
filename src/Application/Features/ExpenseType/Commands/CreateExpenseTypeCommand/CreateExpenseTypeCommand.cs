using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.Features.ExpenseType.Commands.CreateExpenseTypeCommand
{
    // Global reference catalog: only an agency or platform administrator manages
    // it, and only when the agency has the Expenses feature (platform admin has
    // no tenant, so the gate passes).
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record CreateExpenseTypeCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
    }

    public class CreateExpenseTypeCommandHandler : IRequestHandler<CreateExpenseTypeCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateExpenseTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateExpenseTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = new ExpenseTypeEntity
            {
                Name = request.Name,
                WithNotif = request.WithNotif,
                AfterKilometer = request.AfterKilometer,
                AfterMonth = request.AfterMonth,
                IsActive = true,
            };

            _context.ExpenseTypes.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.ExpenseType.Commands.CreateExpenseTypeCommand
{
    public class CreateExpenseTypeCommandValidator : AbstractValidator<CreateExpenseTypeCommand>
    {
        public CreateExpenseTypeCommandValidator()
        {
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
            RuleFor(v => v.AfterKilometer).GreaterThan(0).When(v => v.AfterKilometer.HasValue);
            RuleFor(v => v.AfterMonth).GreaterThan(0).When(v => v.AfterMonth.HasValue);
        }
    }
}
