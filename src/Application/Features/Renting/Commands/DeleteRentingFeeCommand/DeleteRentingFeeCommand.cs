using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Renting.Commands.DeleteRentingFeeCommand
{
    // Takes a charge back off a hire — mistyped at the desk, or the damage turned
    // out to be already on file. A charge is not a financial record of its own
    // (no money moved; see P.11), so it is deleted rather than reversed; what was
    // collected against it stays in the payment ledger and simply becomes credit
    // on the client's account.
    // The charge is the audited entity, not the hire (see AddRentingFeeCommand) —
    // and here it is what makes the deletion recoverable at all: the row is gone,
    // so the audit's Before is the only remaining record that the client was ever
    // charged it.
    [Authorize(Policy = Permissions.RentingUpdate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("DeleteRentingFee", nameof(Domain.Entities.RentingFee))]
    public record DeleteRentingFeeCommand(int RentingId, int FeeId) : IRequest;

    public class DeleteRentingFeeCommandHandler : IRequestHandler<DeleteRentingFeeCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteRentingFeeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteRentingFeeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .Include(r => r.Fees)
                .FirstOrDefaultAsync(r => r.Id == request.RentingId, cancellationToken);

            Guard.Against.NotFound(request.RentingId, entity);

            var fee = entity.Fees.FirstOrDefault(f => f.Id == request.FeeId);

            Guard.Against.NotFound(request.FeeId, fee);

            // Through the aggregate, which refuses on a hire that may not carry
            // fees at all; removing it from the collection is what marks the row
            // deleted, the relationship being a required one.
            entity.RemoveFee(fee);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Renting.Commands.DeleteRentingFeeCommand
{
    public class DeleteRentingFeeCommandValidator : AbstractValidator<DeleteRentingFeeCommand>
    {
        public DeleteRentingFeeCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.FeeId).GreaterThan(0);
        }
    }
}
