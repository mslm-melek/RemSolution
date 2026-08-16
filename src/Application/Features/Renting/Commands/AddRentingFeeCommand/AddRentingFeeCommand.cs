using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Renting.Commands.AddRentingFeeCommand
{
    // Books one extra charge on a hire after the fact — the damage the workshop
    // found the next morning, the fuel nobody checked at the desk. The return
    // dialog does not come through here: it sends its fees with the transition
    // that closes the hire (see ChangeRentingStateCommand.Fees) so the two cannot
    // half-land.
    //
    // Auditable, and gated on Renting.Update rather than a permission of its own:
    // this changes what the client owes, and whoever may correct a hire's price
    // may correct its charges.
    //
    // The audited entity is the CHARGE, not the hire: this save leaves the
    // Renting row untouched, and the interceptor keeps only the entries whose
    // type matches this name (see AuditSaveChangesInterceptor.CaptureAudit), so
    // naming the hire here would record nothing at all.
    [Authorize(Policy = Permissions.RentingUpdate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("AddRentingFee", nameof(Domain.Entities.RentingFee))]
    public record AddRentingFeeCommand : IRequest<int>
    {
        public int RentingId { get; init; }
        public RentingFeePayload Fee { get; init; } = new();
    }

    public class AddRentingFeeCommandHandler : IRequestHandler<AddRentingFeeCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public AddRentingFeeCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task<int> Handle(AddRentingFeeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .Include(r => r.Fees)
                .FirstOrDefaultAsync(r => r.Id == request.RentingId, cancellationToken);

            Guard.Against.NotFound(request.RentingId, entity);

            var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

            var fee = entity.AddFee(
                request.Fee.Kind,
                Domain.ValueObjects.Money.Of(
                    request.Fee.Amount, RentingFees.CurrencyOf(entity, settings.CurrencyCode)),
                request.Fee.Note);

            await _context.SaveChangesAsync(cancellationToken);

            return fee.Id;
        }
    }
}

namespace RemSolution.Application.Features.Renting.Commands.AddRentingFeeCommand
{
    public class AddRentingFeeCommandValidator : AbstractValidator<AddRentingFeeCommand>
    {
        public AddRentingFeeCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.Fee).NotNull().SetValidator(new RentingFeePayloadValidator());
        }
    }
}
