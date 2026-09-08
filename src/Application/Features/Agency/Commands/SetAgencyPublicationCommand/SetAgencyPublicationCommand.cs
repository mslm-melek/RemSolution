using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Agency.Commands.SetAgencyPublicationCommand
{
    /// <summary>
    /// Puts an agency on the public marketplace, or takes it off again. The last
    /// step of the opening wizard, and the switch on the agency's own page.
    /// <para>
    /// One command for both directions: it is one decision with two answers, and
    /// splitting it would let the two drift apart on who may take it.
    /// </para>
    /// </summary>
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("SetAgencyPublication", "Agency")]
    public record SetAgencyPublicationCommand(int Id, bool Published) : IRequest;

    public class SetAgencyPublicationCommandHandler : IRequestHandler<SetAgencyPublicationCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public SetAgencyPublicationCommandHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task Handle(SetAgencyPublicationCommand request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, agency);

            if (!request.Published)
            {
                // Always allowed, and it cancels nothing: the bookings already
                // made are between the customer and the agency.
                agency.Unpublish();
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            if (!await HasSomethingToOfferAsync(request.Id, cancellationToken))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This agency has no car on offer yet — an empty shopfront is worse than none.")
                });
            }

            agency.Publish(_dateTime.GetUtcNow().UtcDateTime);

            await _context.SaveChangesAsync(cancellationToken);
        }

        // Car is an ITenantEntity and the caller has no tenant of their own, so
        // the read acts as the agency (see GetAgencyBranchesQuery). The condition
        // is the car half of MarketplaceCars.Offered — publishing an agency whose
        // every car is archived, unpriced or off the road would list a shopfront
        // with nothing in it.
        private async Task<bool> HasSomethingToOfferAsync(int agencyId, CancellationToken cancellationToken)
        {
            using var _ = AmbientTenant.Push(agencyId);

            return await _context.Cars
                .AsNoTracking()
                .AnyAsync(c => c.Status == CarStatus.Active && c.DailyRate != null, cancellationToken);
        }
    }
}
