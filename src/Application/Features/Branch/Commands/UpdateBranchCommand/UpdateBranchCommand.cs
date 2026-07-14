using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Branch.Commands.UpdateBranchCommand
{
    [Authorize(Roles = Roles.AgencyAdministrator)]
    [RequiresFeature(FeatureFlags.Branches)]
    public record UpdateBranchCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateBranchCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Branches
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.CountryId = request.CountryId;
            entity.Location = BranchLocation.ToPoint(request.Latitude, request.Longitude);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
