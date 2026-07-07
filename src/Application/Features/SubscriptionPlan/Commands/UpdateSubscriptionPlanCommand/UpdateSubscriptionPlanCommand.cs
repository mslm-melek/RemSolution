using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.UpdateSubscriptionPlanCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record UpdateSubscriptionPlanCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public decimal Price { get; init; }
    }

    public class UpdateSubscriptionPlanCommandHandler : IRequestHandler<UpdateSubscriptionPlanCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateSubscriptionPlanCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateSubscriptionPlanCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.SubscriptionPlans
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.MaxCars = request.MaxCars;
            entity.MaxClients = request.MaxClients;
            entity.Price = request.Price;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
