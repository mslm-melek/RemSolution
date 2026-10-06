using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.Features.ExpenseType.Commands.CreateExpenseTypeCommand
{
    // The agency's own catalog: only its administrator manages it (or the
    // platform administrator inside the agency's workspace), and only when the
    // agency has the Expenses feature. The platform-wide entries are
    // ExpenseTypeTemplates, managed through their own commands.
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
        private readonly ITenantProvider _tenant;

        public CreateExpenseTypeCommandHandler(IApplicationDbContext context, ITenantProvider tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        public async Task<int> Handle(CreateExpenseTypeCommand request, CancellationToken cancellationToken)
        {
            // A platform administrator outside any workspace has no agency to add to.
            if (_tenant.AgencyId is null)
            {
                throw new ForbiddenAccessException();
            }

            var entity = new ExpenseTypeEntity { IsActive = true };
            entity.Describe(
                request.Name.Trim(), request.WithNotif, request.AfterKilometer, request.AfterMonth);

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
