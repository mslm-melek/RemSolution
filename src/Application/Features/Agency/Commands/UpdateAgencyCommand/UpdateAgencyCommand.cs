using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.UpdateAgencyCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record UpdateAgencyCommand : IRequest
    {
        public int Id { get; init; }
        // The row version the client last read; the update targets exactly that
        // version so a concurrent change surfaces as a 409 (see P.8).
        public byte[]? RowVersion { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Address { get; init; }
        public int CountryId { get; init; }
        // Persisted to the agency's AgencySettings row (see P.9).
        public string Currency { get; init; } = "TND";
        public int CancellationWindowHours { get; init; } = 24;
        public int ReservationExpiryHours { get; init; } = 48;
    }

    public class UpdateAgencyCommandHandler : IRequestHandler<UpdateAgencyCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public UpdateAgencyCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(UpdateAgencyCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Agencies
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.Name = request.Name;
            entity.Email = request.Email;
            entity.PhoneNumber = request.PhoneNumber;
            entity.Address = request.Address;
            entity.CountryId = request.CountryId;

            var settings = await _context.AgencySettings
                .FirstOrDefaultAsync(s => s.AgencyId == request.Id, cancellationToken);
            Guard.Against.NotFound(request.Id, settings);

            settings.CurrencyCode = request.Currency.Trim().ToUpperInvariant();
            settings.CancellationWindowHours = request.CancellationWindowHours;
            settings.ReservationExpiryHours = request.ReservationExpiryHours;

            await _context.SaveChangesAsync(cancellationToken);

            // The cached snapshot is now stale; next read reloads.
            _settings.Invalidate(request.Id);
        }
    }
}
