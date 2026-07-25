using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Renting.Commands.CancelRentingCommand
{
    // "Delete" for a renting is a cancellation: the row is a financial record and
    // is never physically removed (P.11). Bound to the RentingDelete permission.
    [Authorize(Policy = Permissions.RentingDelete)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("CancelRenting", "Renting")]
    public record CancelRentingCommand(int Id, byte[]? RowVersion = null) : IRequest;

    public class CancelRentingCommandHandler : IRequestHandler<CancelRentingCommand>
    {
        private readonly IApplicationDbContext _context;

        public CancelRentingCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(CancelRentingCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.RentingState is RentingState.Done or RentingState.Cancelled)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "A completed or already-cancelled renting cannot be cancelled.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.RentingState = RentingState.Cancelled;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
