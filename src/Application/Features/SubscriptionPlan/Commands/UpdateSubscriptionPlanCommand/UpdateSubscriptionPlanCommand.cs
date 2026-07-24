using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.UpdateSubscriptionPlanCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("UpdateSubscriptionPlan", "SubscriptionPlan")]
    public record UpdateSubscriptionPlanCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int MaxCars { get; init; }
        public int MaxClients { get; init; }
        public int MaxUsers { get; init; }
        public decimal Price { get; init; }
        // Full replacement set of feature keys this plan unlocks.
        public string[] Features { get; init; } = Array.Empty<string>();
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
                .Include(p => p.Features)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.MaxCars = request.MaxCars;
            entity.MaxClients = request.MaxClients;
            entity.MaxUsers = request.MaxUsers;
            entity.Price = request.Price;

            // Sync the feature rows to the requested set.
            var requested = request.Features.Distinct().ToHashSet();
            entity.Features.Where(f => !requested.Contains(f.Feature)).ToList()
                .ForEach(f => entity.Features.Remove(f));

            var existing = entity.Features.Select(f => f.Feature).ToHashSet();
            foreach (var feature in requested.Where(f => !existing.Contains(f)))
            {
                entity.Features.Add(new PlanFeature { Feature = feature });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
