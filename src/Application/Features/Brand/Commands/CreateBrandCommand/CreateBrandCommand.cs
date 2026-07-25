using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Brand.Commands.CreateBrandCommand
{
    // Reference catalog: managed only by an agency or platform administrator,
    // and only where the Cars module is enabled (platform admin has no tenant,
    // so the feature gate passes).
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record CreateBrandCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
    }
    public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateBrandCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            var entity = new Domain.Entities.Brand
            {
                Name = request.Name
            };

            _context.Brands.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
