using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.SetAgencyInvoiceSettingsCommand
{
    /// <summary>
    /// What makes the agency's invoices legal documents: its tax number, the VAT
    /// rate it charges and the duty stamp. Set while the agency is being opened,
    /// because an agency that starts hiring before these are right issues
    /// invoices it cannot reissue — each one freezes its own copy at issue (see
    /// <c>AgencySettings</c>).
    /// </summary>
    /// <remarks>
    /// Narrow on purpose rather than folded into <c>UpdateAgencyCommand</c>: that
    /// command backs the edit form, which has no tax fields, so widening it would
    /// have every save from that form blank these three.
    /// <para>
    /// AgencySettings is not an <c>ITenantEntity</c> — it hangs off the agency —
    /// so this needs no tenant push, only the explicit AgencyId below.
    /// </para>
    /// </remarks>
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("SetAgencyInvoiceSettings", "Agency")]
    public record SetAgencyInvoiceSettingsCommand : IRequest
    {
        public int AgencyId { get; init; }
        public string? TaxIdentifier { get; init; }
        public decimal VatRatePercent { get; init; }
        public decimal FiscalStampAmount { get; init; }
    }

    public class SetAgencyInvoiceSettingsCommandHandler
        : IRequestHandler<SetAgencyInvoiceSettingsCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public SetAgencyInvoiceSettingsCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(
            SetAgencyInvoiceSettingsCommand request, CancellationToken cancellationToken)
        {
            var settings = await _context.AgencySettings
                .FirstOrDefaultAsync(s => s.AgencyId == request.AgencyId, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, settings);

            settings.TaxIdentifier = string.IsNullOrWhiteSpace(request.TaxIdentifier)
                ? null
                : request.TaxIdentifier.Trim();
            settings.VatRatePercent = request.VatRatePercent;
            settings.FiscalStampAmount = request.FiscalStampAmount;

            await _context.SaveChangesAsync(cancellationToken);

            // The snapshot is cached for ten minutes, and each Facture freezes a
            // copy of the rate at issue — an invoice issued on the stale figure
            // says 19% for good, and a reprint has to say what was charged.
            _settings.Invalidate(request.AgencyId);
        }
    }
}

namespace RemSolution.Application.Features.Agency.Commands.SetAgencyInvoiceSettingsCommand
{
    public class SetAgencyInvoiceSettingsCommandValidator
        : AbstractValidator<SetAgencyInvoiceSettingsCommand>
    {
        public SetAgencyInvoiceSettingsCommandValidator()
        {
            RuleFor(v => v.AgencyId).GreaterThan(0);
            RuleFor(v => v.TaxIdentifier).MaximumLength(40);
            // The same bounds the agency's own screen applies (see
            // UpdateMyAgencyCommandValidator): a rate is a percentage.
            RuleFor(v => v.VatRatePercent).InclusiveBetween(0m, 100m);
            RuleFor(v => v.FiscalStampAmount).GreaterThanOrEqualTo(0m);
        }
    }
}
