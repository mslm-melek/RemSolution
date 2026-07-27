using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = RemSolution.Domain.Entities.Payment;

namespace RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand
{
    // Records a payment (or, with IsRefund, a refund) against exactly one of a
    // renting, a reservation, or a client directly. Amount is always supplied
    // positive; a refund is stored as a negative entry so the running net
    // collected is a simple sum of all entries (reversals are negative too).
    //
    // Invariant (P.3.4): the net collected against a booking may never exceed its
    // agreed Price, and a refund may not take it below zero. When a reservation
    // reaches full settlement it auto-transitions Confirmed → Paid.
    [Authorize(Policy = Permissions.PaymentCreate)]
    [RequiresFeature(FeatureFlags.Payments)]
    public record CreatePaymentCommand : IRequest<int>
    {
        public int? RentingId { get; init; }
        public int? ReservationId { get; init; }
        // Only for a standalone client payment (no booking target).
        public int? ClientId { get; init; }
        public decimal Amount { get; init; }
        public bool IsRefund { get; init; }
        public PaymentMethod Method { get; init; } = PaymentMethod.Cash;
        public DateTime? PayementDate { get; init; }
        public string? Notes { get; init; }
    }

    public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public CreatePaymentCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
        {
            int agencyId;
            int? clientId;
            decimal? price = null;      // agreed price of the booking, if any
            decimal currentNet = 0m;    // net already collected against it

            Domain.Entities.Reservation? reservation = null;

            if (request.RentingId is int rentingId)
            {
                var renting = await _context.Rentings
                    .FirstOrDefaultAsync(r => r.Id == rentingId, cancellationToken);
                Guard.Against.NotFound(rentingId, renting);

                agencyId = renting.AgencyId;
                clientId = renting.ClientId;
                price = renting.Price?.Amount;
                currentNet = await _context.Payments
                    .Where(p => p.RentingId == rentingId && p.PayementAmount != null)
                    .SumAsync(p => p.PayementAmount!.Amount, cancellationToken);
            }
            else if (request.ReservationId is int reservationId)
            {
                reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
                Guard.Against.NotFound(reservationId, reservation);

                if (reservation.Status is not (ReservationStatus.Confirmed or ReservationStatus.Paid))
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.ReservationId),
                            "Payments can only be recorded against a confirmed reservation.")
                    });
                }

                agencyId = reservation.AgencyId;
                clientId = reservation.ClientId;
                price = reservation.Price?.Amount;
                currentNet = await _context.Payments
                    .Where(p => p.ReservationId == reservationId && p.PayementAmount != null)
                    .SumAsync(p => p.PayementAmount!.Amount, cancellationToken);
            }
            else
            {
                var targetClientId = request.ClientId!.Value;
                var client = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Id == targetClientId, cancellationToken);
                Guard.Against.NotFound(targetClientId, client);

                agencyId = client.AgencyId;
                clientId = client.Id;
            }

            var signed = request.IsRefund ? -request.Amount : request.Amount;
            var netAfter = currentNet + signed;

            if (!request.IsRefund && price is decimal cap && netAfter > cap)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Amount),
                        $"This payment would exceed the agreed price. Outstanding balance is {cap - currentNet}.")
                });
            }

            if (request.IsRefund && netAfter < 0m)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Amount),
                        $"A refund cannot exceed the amount collected ({currentNet}).")
                });
            }

            var settings = await _settings.GetAsync(agencyId, cancellationToken);

            var entity = new PaymentEntity
            {
                RentingId = request.RentingId,
                ReservationId = request.ReservationId,
                ClientId = clientId,
                PayementDate = request.PayementDate ?? _dateTime.GetUtcNow().UtcDateTime,
                PayementAmount = Money.Of(signed, settings.CurrencyCode),
                Method = request.Method,
                IsRefund = request.IsRefund,
                Notes = request.Notes,
            };

            _context.Payments.Add(entity);

            // Keep the reservation's denormalised running total in step, and flip
            // it to Paid once fully settled.
            if (reservation is not null)
            {
                reservation.PayedPrice = Money.Of(netAfter, settings.CurrencyCode);

                if (reservation.Status == ReservationStatus.Confirmed
                    && price is decimal full && netAfter >= full)
                {
                    reservation.MarkPaid();
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand
{
    public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
    {
        public CreatePaymentCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Amount).GreaterThan(0);
            RuleFor(v => v.Method).IsInEnum();
            RuleFor(v => v.Notes).MaximumLength(1000);

            // Exactly one target: a renting, a reservation, or a client.
            RuleFor(v => v)
                .Must(v => TargetCount(v) == 1)
                .WithMessage(_ => localizer["Validation.Payment.SingleTarget"]);
        }

        private static int TargetCount(CreatePaymentCommand v)
            => (v.RentingId.HasValue ? 1 : 0)
             + (v.ReservationId.HasValue ? 1 : 0)
             + (v.ClientId.HasValue ? 1 : 0);
    }
}
