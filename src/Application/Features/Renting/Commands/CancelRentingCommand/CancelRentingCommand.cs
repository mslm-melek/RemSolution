using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;
using PaymentEntity = RemSolution.Domain.Entities.Payment;

namespace RemSolution.Application.Features.Renting.Commands.CancelRentingCommand
{
    // "Delete" for a renting is a cancellation: the row is a financial record and
    // is never physically removed (P.11). Bound to the RentingDelete permission.
    //
    // Cancelling is also a money decision, and the two halves of it are asked for
    // here rather than left implicit: what the client still owes (CancellationFee)
    // and whether the excess they have already paid goes back to them now
    // (RefundExcess). Neither has a silent default that moves money — omitting
    // both cancels for free and leaves whatever was collected sitting as a credit
    // on the client's account, which is exactly what this command did before the
    // fee existed.
    [Authorize(Policy = Permissions.RentingDelete)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("CancelRenting", "Renting")]
    public record CancelRentingCommand : IRequest
    {
        public int Id { get; init; }
        public byte[]? RowVersion { get; init; }

        /// <summary>
        /// What the agency keeps for calling the hire off, and therefore what the
        /// client still owes on it (see Renting.CancellationFee). Null or zero
        /// cancels for free: the whole price comes off their balance. Never more
        /// than the price — a cancelled hire cannot cost more than the hire.
        /// </summary>
        public decimal? CancellationFee { get; init; }

        /// <summary>
        /// Whether to hand back what the client has paid beyond the fee, as part
        /// of this cancellation. True records the refund on the ledger now — the
        /// money is going back across the counter. False leaves it as a credit on
        /// their account, for the agency to settle or apply later; the credits
        /// screen shows it either way, which is the point of not guessing.
        /// <para>
        /// The amount is computed here, not accepted from the caller: it is
        /// whatever was collected less the fee being kept, and nothing else.
        /// </para>
        /// </summary>
        public bool RefundExcess { get; init; }

        public PaymentMethod RefundMethod { get; init; } = PaymentMethod.Cash;
    }

    public class CancelRentingCommandHandler : IRequestHandler<CancelRentingCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly IIdentityService _identityService;
        private readonly IUser _user;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CancelRentingCommandHandler(
            IApplicationDbContext context,
            IAgencySettingsProvider settings,
            IIdentityService identityService,
            IUser user,
            ITenantProvider tenant,
            TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _identityService = identityService;
            _user = user;
            _tenant = tenant;
            _dateTime = dateTime;
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

            var fee = request.CancellationFee ?? 0m;

            if (fee > 0m)
            {
                // A hire that charges nothing cannot charge for being called off:
                // the fee is a part of the price kept, and there is no price to
                // take it from (a priceless renting has no balance at all — see
                // RentingDto.Outstanding).
                if (entity.Price is not Money price)
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.CancellationFee),
                            "This renting carries no price, so no cancellation fee can be charged on it.")
                    });
                }

                if (fee > price.Amount)
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.CancellationFee),
                            $"The cancellation fee cannot exceed the agreed price ({price.Amount}).")
                    });
                }
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            // The currency is the booking's own, falling back to the agency's for a
            // hire that carries no price — same rule as everywhere money is written.
            var currency = entity.Price?.Currency
                ?? (await _settings.GetAsync(entity.AgencyId, cancellationToken)).CurrencyCode;

            entity.RentingState = RentingState.Cancelled;
            entity.CancellationFee = fee > 0m ? Money.Of(fee, currency).Round() : null;

            if (request.RefundExcess)
            {
                // Handing money back is a payment-ledger write, so it answers to
                // the Payments module's own gate rather than riding in on
                // Renting.Delete (see Entitlements).
                await Entitlements.EnsureAsync(
                    _user, _identityService, _context, _tenant, _dateTime,
                    Permissions.PaymentCreate, FeatureFlags.Payments, cancellationToken);

                var collected = await _context.Payments
                    .Where(p => p.RentingId == entity.Id && p.PayementAmount != null)
                    .SumAsync(p => p.PayementAmount!.Amount, cancellationToken);

                var refundable = collected - fee;

                if (refundable > 0m)
                {
                    // Negative entry, like every refund (see CreatePaymentCommand),
                    // so the net collected against this hire lands exactly on the
                    // fee that is being kept.
                    _context.Payments.Add(new PaymentEntity
                    {
                        AgencyId = entity.AgencyId,
                        RentingId = entity.Id,
                        ClientId = entity.ClientId,
                        PayementDate = _dateTime.GetUtcNow().UtcDateTime,
                        PayementAmount = Money.Of(-refundable, currency).Round(),
                        Method = request.RefundMethod,
                        IsRefund = true,
                        Notes = "Cancellation refund",
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public class CancelRentingCommandValidator : AbstractValidator<CancelRentingCommand>
    {
        public CancelRentingCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            // Zero is allowed and means the same as omitting it: cancelled for free.
            RuleFor(v => v.CancellationFee)
                .GreaterThanOrEqualTo(0).When(v => v.CancellationFee.HasValue);
            RuleFor(v => v.RefundMethod).IsInEnum();
        }
    }
}
