using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.CreateSubscriptionPlanCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("CreateSubscriptionPlan", "SubscriptionPlan")]
    public record CreateSubscriptionPlanCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public int MaxUsers { get; init; }
        public decimal Price { get; init; }
        // Feature keys this plan unlocks (see FeatureFlags).
        public string[] Features { get; init; } = Array.Empty<string>();
    }

    public class CreateSubscriptionPlanCommandHandler : IRequestHandler<CreateSubscriptionPlanCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateSubscriptionPlanCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateSubscriptionPlanCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.SubscriptionPlan
            {
                Name = request.Name,
                MaxCars = request.MaxCars,
                MaxClients = request.MaxClients,
                MaxUsers = request.MaxUsers,
                Price = request.Price,
                Features = request.Features
                    .Distinct()
                    .Select(f => new RemSolution.Domain.Entities.PlanFeature { Feature = f })
                    .ToList()
            };

            _context.SubscriptionPlans.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
