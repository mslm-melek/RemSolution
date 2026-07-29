using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.UpdateMyAgencyCommand
{
    /// <summary>
    /// An agency administrator edits their OWN agency: the details customers see
    /// and the booking rules. The tenant comes from the caller's claim, never
    /// from the request, so there is no agency id to send.
    /// <para>
    /// Deliberately narrower than <c>UpdateAgencyCommand</c>: the currency is not
    /// editable here. Every Money amount the agency has already stored is in the
    /// old code, and changing it would silently reinterpret all of them rather
    /// than convert anything — that stays with the platform administrator.
    /// </para>
    /// </summary>
    [Authorize(Roles = Roles.AgencyAdministrator)]
    [Auditable("UpdateMyAgency", "Agency")]
    public record UpdateMyAgencyCommand : IRequest
    {
        // The row version the client last read; the update targets exactly that
        // version so a concurrent change surfaces as a 409 (see P.8).
        public byte[]? RowVersion { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Address { get; init; }
        // The HQ pin for the address above, as picked on the map. Set as a pair
        // or not at all (see the validator).
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public int CountryId { get; init; }
        public int CancellationWindowHours { get; init; } = 24;
        public int ReservationExpiryHours { get; init; } = 48;
    }

    public class UpdateMyAgencyCommandHandler : IRequestHandler<UpdateMyAgencyCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly ITenantProvider _tenant;

        public UpdateMyAgencyCommandHandler(
            IApplicationDbContext context,
            IAgencySettingsProvider settings,
            ITenantProvider tenant)
        {
            _context = context;
            _settings = settings;
            _tenant = tenant;
        }

        public async Task Handle(UpdateMyAgencyCommand request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var entity = await _context.Agencies
                .FindAsync(new object[] { agencyId }, cancellationToken);

            Guard.Against.NotFound(agencyId, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.Name = request.Name;
            entity.Email = request.Email;
            entity.PhoneNumber = request.PhoneNumber;
            entity.Address = request.Address;
            entity.Location = GeoPoint.ToPoint(request.Latitude, request.Longitude);
            entity.CountryId = request.CountryId;

            var settings = await _context.AgencySettings
                .FirstOrDefaultAsync(s => s.AgencyId == agencyId, cancellationToken);
            Guard.Against.NotFound(agencyId, settings);

            settings.CancellationWindowHours = request.CancellationWindowHours;
            settings.ReservationExpiryHours = request.ReservationExpiryHours;

            await _context.SaveChangesAsync(cancellationToken);

            // The cached snapshot is now stale; next read reloads.
            _settings.Invalidate(agencyId);
        }
    }
}
