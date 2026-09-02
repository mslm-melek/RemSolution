using FluentValidation.Results;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.Renting.Commands.SettleRentingDepositCommand
{
    /// <summary>
    /// Says what became of a deposit after the fact — the car came back on a busy
    /// afternoon and the question was left open, or the damage estimate arrived
    /// from the garage a week later.
    /// <para>
    /// The return itself settles the deposit inline (see
    /// ChangeRentingStateCommand); this exists because in practice it will
    /// sometimes not, and a deposit with no recorded fate is the defect this
    /// whole pair of paths is here to prevent.
    /// </para>
    /// <para>
    /// Carries Payment.Create rather than Renting.Update: what it actually does
    /// is hand money back, and it writes a refund to the ledger to prove it.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.PaymentCreate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("SettleRentingDeposit", "Renting")]
    public record SettleRentingDepositCommand : IRequest
    {
        public int RentingId { get; init; }
        public byte[]? RowVersion { get; init; }
        public DepositSettlementPayload Settlement { get; init; } = new();
    }

    public class SettleRentingDepositCommandHandler : IRequestHandler<SettleRentingDepositCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public SettleRentingDepositCommandHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task Handle(SettleRentingDepositCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.RentingId, cancellationToken);

            Guard.Against.NotFound(request.RentingId, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            if (!entity.HasUnsettledDeposit)
            {
                // Refused rather than treated as idempotent: a second settlement
                // would be a second refund, and paying a deposit back twice is
                // exactly the mistake this command is supposed to prevent.
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Settlement),
                        entity.DepositAmount is null or { Amount: <= 0m }
                            ? "This hire took no deposit."
                            : "This hire's deposit has already been settled.")
                });
            }

            var refund = DepositSettlements.Settle(
                entity, request.Settlement, _dateTime.GetUtcNow().UtcDateTime);

            if (refund is not null)
            {
                _context.Payments.Add(refund);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public class SettleRentingDepositCommandValidator : AbstractValidator<SettleRentingDepositCommand>
    {
        public SettleRentingDepositCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.Settlement).NotNull();
            RuleFor(v => v.Settlement).SetValidator(new DepositSettlementPayloadValidator());
        }
    }
}
