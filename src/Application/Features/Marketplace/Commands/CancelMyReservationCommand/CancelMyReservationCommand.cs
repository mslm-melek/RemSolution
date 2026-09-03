using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand
{
    // A customer calls off one of their own bookings — a request still waiting for
    // an answer, or a hold the agency has already confirmed. The second is the
    // case that costs: past the agency's free window it carries a cancellation fee
    // (see CancellationPolicy), and it counts against the customer's reliability,
    // which a request the agency never answered does not.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record CancelMyReservationCommand(int Id, string? Reason = null) : IRequest
    {
        public const string ReasonPrefix = "Cancelled by the customer: ";

        // The stored reason carries the prefix, so what the customer may type is
        // Reservation.CancelledReason's column length minus it — refused here
        // rather than truncated by SQL Server on the way in.
        public static readonly int MaxReasonLength = 1000 - ReasonPrefix.Length;
    }

    public class CancelMyReservationCommandHandler : IRequestHandler<CancelMyReservationCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public CancelMyReservationCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings,
            IUser user, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task Handle(CancelMyReservationCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var reservation = await _context.Reservations
                .IgnoreQueryFilters()
                .Include(r => r.Client)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, reservation);

            if (reservation.Client?.MarketplaceUserId != userId)
            {
                throw new ForbiddenAccessException();
            }

            var settings = await _settings.GetAsync(reservation.AgencyId, cancellationToken);
            var policy = settings.CancellationPolicy;
            var now = _dateTime.GetUtcNow().UtcDateTime;

            // The same hard cutoff the agency's own cancel path enforces: once a
            // booking is within CancellationWindowHours of its start it can no
            // longer be called off at all, by either side.
            if (reservation.StartDate is DateTime start
                && now >= start.AddHours(-settings.CancellationWindowHours))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        $"This reservation can no longer be cancelled — it is within " +
                        $"{settings.CancellationWindowHours}h of its start.")
                });
            }

            // Act as the reservation's agency so the tenant write-stamp check on
            // the modified row passes.
            using var _ = AmbientTenant.Push(reservation.AgencyId);

            var fee = policy.FeeFor(
                reservation.Price, reservation.StartDate, now, settings.CurrencyCode);

            // Throws InvalidReservationTransitionException (→ 409) if the hold has
            // already been converted, refused, expired or cancelled.
            reservation.Cancel(
                Reason(request.Reason), at: now, byCustomer: true, fee: fee);

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string Reason(string? reason) =>
            string.IsNullOrWhiteSpace(reason)
                ? "Cancelled by the customer."
                : CancelMyReservationCommand.ReasonPrefix + reason.Trim();
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand
{
    public class CancelMyReservationCommandValidator : AbstractValidator<CancelMyReservationCommand>
    {
        public CancelMyReservationCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Reason)
                .MaximumLength(CancelMyReservationCommand.MaxReasonLength);
        }
    }
}
