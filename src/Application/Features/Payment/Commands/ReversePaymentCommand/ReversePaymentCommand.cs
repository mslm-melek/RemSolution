using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;
using PaymentEntity = RemSolution.Domain.Entities.Payment;

namespace RemSolution.Application.Features.Payment.Commands.ReversePaymentCommand
{
    // "Delete" for a payment is a reversal: a payment is a financial record and
    // is never physically removed (P.11). This posts an offsetting negative entry
    // that references the original, leaving both on the ledger. Bound to
    // PaymentDelete.
    [Authorize(Policy = Permissions.PaymentDelete)]
    [RequiresFeature(FeatureFlags.Payments)]
    [Auditable("ReversePayment", "Payment")]
    public record ReversePaymentCommand(int Id) : IRequest<int>;

    public class ReversePaymentCommandHandler : IRequestHandler<ReversePaymentCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public ReversePaymentCommandHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(ReversePaymentCommand request, CancellationToken cancellationToken)
        {
            var original = await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, original);

            if (original.ReversesPaymentId is not null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id), "A reversal entry cannot itself be reversed.")
                });
            }

            var alreadyReversed = await _context.Payments
                .AnyAsync(p => p.ReversesPaymentId == original.Id, cancellationToken);

            if (alreadyReversed)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id), "This payment has already been reversed.")
                });
            }

            if (original.PayementAmount is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id), "The payment has no amount to reverse.")
                });
            }

            var reversal = new PaymentEntity
            {
                RentingId = original.RentingId,
                ReservationId = original.ReservationId,
                ClientId = original.ClientId,
                // .Date: PayementDate is a calendar day at UTC midnight, never an
                // instant (see CreatePaymentCommand).
                PayementDate = _dateTime.GetUtcNow().UtcDateTime.Date,
                PayementAmount = Money.Of(-original.PayementAmount.Amount, original.PayementAmount.Currency),
                Method = original.Method,
                Notes = $"Reversal of payment #{original.Id}",
                ReversesPaymentId = original.Id,
            };

            _context.Payments.Add(reversal);

            // Keep the reservation's denormalised running total in step (the
            // reversal removes the original entry's contribution).
            if (original.ReservationId is int reservationId)
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

                if (reservation is not null)
                {
                    var previous = reservation.PayedPrice?.Amount ?? 0m;
                    reservation.PayedPrice = Money.Of(
                        previous - original.PayementAmount.Amount, original.PayementAmount.Currency);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return reversal.Id;
        }
    }
}
