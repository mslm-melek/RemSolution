using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Commands.DeleteCarUnavailabilityCommand
{
    /// <summary>
    /// Puts the car back on the road — the job was cancelled, or the block was a
    /// mistake. Physically removed, not archived: a plan that never happened is
    /// not a record worth keeping, and the audit entry says who removed it.
    /// </summary>
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("DeleteCarUnavailability", "Car")]
    public record DeleteCarUnavailabilityCommand(int Id) : IRequest;

    public class DeleteCarUnavailabilityCommandHandler
        : IRequestHandler<DeleteCarUnavailabilityCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteCarUnavailabilityCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(
            DeleteCarUnavailabilityCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CarUnavailabilities
                .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // No lock and no transaction: freeing dates cannot create a conflict,
            // and the availability check the booking paths run happens under their
            // own lock afterwards.
            _context.CarUnavailabilities.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
