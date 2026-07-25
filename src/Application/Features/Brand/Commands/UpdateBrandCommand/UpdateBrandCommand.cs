using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Brand.Commands.UpdateBrandCommand
{
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record UpdateBrandCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateBrandCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Brands
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
