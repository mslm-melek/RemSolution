using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using RentingEntity = RemSolution.Domain.Entities.Renting;

namespace RemSolution.Application.Features.Reservation.Commands.ConvertReservationCommand
{
    // Converts a Confirmed/Paid hold into an actual renting (the aggregate's
    // Convert() guards the state). Carries the hold's dates, snapshot price and
    // deposit onto the renting, and moves any advance payments across.
    //
    // Client dedup (P.3.3): the agent may supply the driver's real CIN/passport
    // at pickup. If another client in the agency already carries that document we
    // link the renting to THAT client (and fold the marketplace account onto it)
    // rather than leaving a duplicate; otherwise we enrich the hold's own client
    // with the supplied document.
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("ConvertReservation", "Reservation")]
    public record ConvertReservationCommand : IRequest<int>
    {
        public int Id { get; init; }
        public byte[]? RowVersion { get; init; }
        // Optional identity captured at pickup, used for dedup + enrichment.
        public string? CIN { get; init; }
        public string? PasseportNumber { get; init; }
    }

    public class ConvertReservationCommandHandler : IRequestHandler<ConvertReservationCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAvailabilityChecker _availability;

        public ConvertReservationCommandHandler(
            IApplicationDbContext context, IAvailabilityChecker availability)
        {
            _context = context;
            _availability = availability;
        }

        public async Task<int> Handle(ConvertReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .Include(r => r.Client)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.CarId is not int carId || entity.StartDate is not DateTime start
                || entity.EndDate is not DateTime end)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "The reservation is missing a car or dates and cannot be converted.")
                });
            }

            if (entity.Client is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "The reservation has no client and cannot be converted.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            // Any OTHER active booking that appeared for this car since the hold
            // was placed blocks the conversion; the hold itself is excluded.
            await _availability.EnsureCarAvailableAsync(
                carId, start, end, excludeRentingId: null, excludeReservationId: entity.Id, cancellationToken);

            var client = await ResolveClientAsync(entity.Client, request, cancellationToken);

            var renting = new RentingEntity
            {
                CarId = carId,
                ClientId = client.Id,
                StartDate = start,
                EndDate = end,
                // Keep the agreed price and deposit from the hold — not re-quoted.
                Price = entity.Price,
                DepositAmount = entity.DepositAmount,
                RentingState = RentingState.NotYet,
                Notes = entity.Notes,
            };

            _context.Rentings.Add(renting);

            // Advance/deposit payments recorded against the hold follow it onto
            // the renting so the renting reflects what has been collected.
            var payments = await _context.Payments
                .Where(p => p.ReservationId == entity.Id)
                .ToListAsync(cancellationToken);

            foreach (var payment in payments)
            {
                payment.Renting = renting;
                payment.ReservationId = null;
                payment.ClientId = client.Id;
            }

            // Aggregate transition: sets Converted, links the renting, raises the event.
            entity.Convert(renting);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return renting.Id;
        }

        // Returns the client the renting should be attached to, applying the
        // CIN/passport dedup rule against the current agency's clients.
        private async Task<Domain.Entities.Client> ResolveClientAsync(
            Domain.Entities.Client holdClient, ConvertReservationCommand request, CancellationToken ct)
        {
            var cin = string.IsNullOrWhiteSpace(request.CIN) ? null : request.CIN.Trim();
            var passport = string.IsNullOrWhiteSpace(request.PasseportNumber) ? null : request.PasseportNumber.Trim();

            if (cin is null && passport is null)
            {
                return holdClient;
            }

            var match = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id != holdClient.Id
                        && ((cin != null && c.CIN == cin) || (passport != null && c.PasseportNumber == passport)),
                    ct);

            if (match is not null)
            {
                // Fold the marketplace account onto the existing client so future
                // self-service bookings resolve to the same row.
                if (match.MarketplaceUserId is null && holdClient.MarketplaceUserId is not null)
                {
                    match.MarketplaceUserId = holdClient.MarketplaceUserId;
                }

                return match;
            }

            // No duplicate: enrich the hold's own client with the captured docs.
            if (cin is not null && string.IsNullOrWhiteSpace(holdClient.CIN))
            {
                holdClient.CIN = cin;
            }
            if (passport is not null && string.IsNullOrWhiteSpace(holdClient.PasseportNumber))
            {
                holdClient.PasseportNumber = passport;
            }

            return holdClient;
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.ConvertReservationCommand
{
    public class ConvertReservationCommandValidator : AbstractValidator<ConvertReservationCommand>
    {
        public ConvertReservationCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.CIN).MaximumLength(50);
            RuleFor(v => v.PasseportNumber).MaximumLength(50);
        }
    }
}
