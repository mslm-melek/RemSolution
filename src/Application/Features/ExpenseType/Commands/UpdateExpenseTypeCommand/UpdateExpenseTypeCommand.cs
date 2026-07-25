using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExpenseType.Commands.UpdateExpenseTypeCommand
{
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record UpdateExpenseTypeCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
    }

    public class UpdateExpenseTypeCommandHandler : IRequestHandler<UpdateExpenseTypeCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateExpenseTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateExpenseTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExpenseTypes
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.IsActive = request.IsActive;
            entity.WithNotif = request.WithNotif;
            entity.AfterKilometer = request.AfterKilometer;
            entity.AfterMonth = request.AfterMonth;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.ExpenseType.Commands.UpdateExpenseTypeCommand
{
    public class UpdateExpenseTypeCommandValidator : AbstractValidator<UpdateExpenseTypeCommand>
    {
        public UpdateExpenseTypeCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
            RuleFor(v => v.AfterKilometer).GreaterThan(0).When(v => v.AfterKilometer.HasValue);
            RuleFor(v => v.AfterMonth).GreaterThan(0).When(v => v.AfterMonth.HasValue);
        }
    }
}
