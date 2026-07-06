using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.CreateSubscriptionPlanCommand
{
    public record CreateSubscriptionPlanCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public decimal Price { get; init; }
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
                Price = request.Price
            };

            _context.SubscriptionPlans.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
