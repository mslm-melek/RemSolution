using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand
{
    // A customer cancels their OWN pending hold. A confirmed reservation has
    // become a renting and can't be cancelled here.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record CancelMyReservationCommand(int Id) : IRequest;

    public class CancelMyReservationCommandHandler : IRequestHandler<CancelMyReservationCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public CancelMyReservationCommandHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
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

            if (reservation.Status != ReservationStatus.PendingConfirmation)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id), "Only a pending reservation can be cancelled.")
                });
            }

            // Act as the reservation's agency so the tenant write-stamp check on
            // the modified row passes.
            using var _ = AmbientTenant.Push(reservation.AgencyId);

            reservation.Cancel("Cancelled by the customer.");

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
