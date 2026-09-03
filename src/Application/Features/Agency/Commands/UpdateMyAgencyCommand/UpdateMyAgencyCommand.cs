using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Enums;
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

        // Notification settings. Defaults mirror AgencySettings so a client that
        // omits them lands on the same values a new agency starts with, rather
        // than on zeros that would silently switch every reminder off.
        public int ExpenseDueLeadDays { get; init; } = 14;
        public int ExpenseDueLeadKilometers { get; init; } = 1000;
        public int ReservationUpcomingLeadDays { get; init; } = 3;
        public int ClientDocumentExpiryLeadDays { get; init; } = 30;
        public bool NotifyStaffByEmail { get; init; } = true;
        public bool NotifyClientsByEmail { get; init; }
        public int ClientReminderDaysBeforeStart { get; init; } = 2;
        public int ClientReminderDaysBeforeEnd { get; init; } = 1;

        // Tax. Editable by the agency administrator because these are their own
        // registration and their own jurisdiction's figures — unlike the currency
        // above, changing them reinterprets nothing already stored: every issued
        // invoice keeps its own frozen copy (see the Facture entity).
        public string? TaxIdentifier { get; init; }
        public decimal VatRatePercent { get; init; } = 19m;
        public decimal FiscalStampAmount { get; init; } = 1m;

        // How long each document stays valid where this agency trades, used to
        // fill in an expiry date the agent did not type. Zero for a document that
        // does not expire there.
        public int CINValidityYears { get; init; } = 10;
        public int PasseportValidityYears { get; init; } = 5;
        public int DrivingLicenceValidityYears { get; init; } = 10;

        // Cancellation policy. Off by default, and deliberately separate from
        // CancellationWindowHours above: that one says when cancelling stops
        // being possible, these say when it stops being free.
        public CancellationFeeMode CancellationFeeMode { get; init; } = CancellationFeeMode.None;
        public decimal CancellationFeeValue { get; init; }
        public int CancellationFreeHours { get; init; } = 48;
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

            settings.ExpenseDueLeadDays = request.ExpenseDueLeadDays;
            settings.ExpenseDueLeadKilometers = request.ExpenseDueLeadKilometers;
            settings.ReservationUpcomingLeadDays = request.ReservationUpcomingLeadDays;
            settings.ClientDocumentExpiryLeadDays = request.ClientDocumentExpiryLeadDays;
            settings.NotifyStaffByEmail = request.NotifyStaffByEmail;
            settings.NotifyClientsByEmail = request.NotifyClientsByEmail;
            settings.ClientReminderDaysBeforeStart = request.ClientReminderDaysBeforeStart;
            settings.ClientReminderDaysBeforeEnd = request.ClientReminderDaysBeforeEnd;

            settings.TaxIdentifier = string.IsNullOrWhiteSpace(request.TaxIdentifier)
                ? null
                : request.TaxIdentifier.Trim();
            settings.VatRatePercent = request.VatRatePercent;
            settings.FiscalStampAmount = request.FiscalStampAmount;

            settings.CINValidityYears = request.CINValidityYears;
            settings.PasseportValidityYears = request.PasseportValidityYears;
            settings.DrivingLicenceValidityYears = request.DrivingLicenceValidityYears;

            settings.CancellationFeeMode = request.CancellationFeeMode;
            settings.CancellationFeeValue = request.CancellationFeeValue;
            settings.CancellationFreeHours = request.CancellationFreeHours;

            await _context.SaveChangesAsync(cancellationToken);

            // The cached snapshot is now stale; next read reloads.
            _settings.Invalidate(agencyId);
        }
    }
}
