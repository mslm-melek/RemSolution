using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Branch.Commands.CreateBranchCommand
{
    [Authorize(Roles = Roles.AgencyAdministrator)]
    [RequiresFeature(FeatureFlags.Branches)]
    public record CreateBranchCommand : IRequest<int>
    {
        // AgencyId is not accepted from the client: TenantEntityInterceptor
        // stamps it from the current tenant on insert.
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateBranchCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Branch
            {
                Name = request.Name,
                CountryId = request.CountryId,
                Location = BranchLocation.ToPoint(request.Latitude, request.Longitude),
            };

            _context.Branches.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
