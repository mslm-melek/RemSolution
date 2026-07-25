using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraService.Commands.DeleteExtraServiceCommand
{
    // An extra service is a renting line item (not a financial ledger record like
    // Payment), so a data-entry mistake is physically removed.
    [Authorize(Policy = Permissions.ExtraServiceDelete)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record DeleteExtraServiceCommand(int Id) : IRequest;

    public class DeleteExtraServiceCommandHandler : IRequestHandler<DeleteExtraServiceCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteExtraServiceCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteExtraServiceCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServices
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.ExtraServices.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
