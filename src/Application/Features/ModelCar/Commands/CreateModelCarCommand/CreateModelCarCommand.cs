using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand
{
    // Reference catalog: managed only by an agency or platform administrator,
    // and only where the Cars module is enabled.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record CreateModelCarCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public int? BrandId { get; init; }
    }
    public class CreateModelCarCommandHandler : IRequestHandler<CreateModelCarCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateModelCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateModelCarCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.ModelCar
            {
                Name = request.Name,
                BrandId = request.BrandId
            };

            _context.ModelCars.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
