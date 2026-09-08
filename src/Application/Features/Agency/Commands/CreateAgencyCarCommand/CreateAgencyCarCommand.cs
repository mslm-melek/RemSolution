using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyCarCommand
{
    /// <summary>
    /// The platform administrator puts the first cars into an agency being set
    /// up — the fleet step of the opening wizard. Deliberately narrow: matricule,
    /// model, where it is collected and what it costs a day. Everything else
    /// about a car is edited by the agency itself, on the screen built for it.
    /// </summary>
    /// <remarks>
    /// Not feature-gated on the agency's plan, for the same reason
    /// <c>GetAgencyBranchesQuery</c> is not: whether the plan includes the Cars
    /// module governs what the agency's staff can reach, not whether the app
    /// owner can set the agency up.
    /// <para>
    /// The tenant push is the ordinary one, NOT the administrative one the
    /// branch commands use: a fleet counts against the plan just assigned, and
    /// exempting it would let an agency be stocked past the quota it is paying
    /// for. The consequence is that a plan has to be assigned first — which is
    /// the step before this one in the wizard.
    /// </para>
    /// </remarks>
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("CreateAgencyCar", "Car")]
    public record CreateAgencyCarCommand : IRequest<int>
    {
        public int AgencyId { get; init; }
        public string Matricule { get; init; } = string.Empty;
        public int? ModelId { get; init; }
        public int? BranchId { get; init; }
        public decimal? DailyRate { get; init; }
    }

    public class CreateAgencyCarCommandHandler : IRequestHandler<CreateAgencyCarCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public CreateAgencyCarCommandHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateAgencyCarCommand request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AgencyId, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, agency);

            // Car is an ITenantEntity and the caller has no tenant of their own,
            // so the write stamp, the quota check and the lock all need the
            // agency pushed (see CreateAgencyBranchCommand).
            using var _ = AmbientTenant.Push(request.AgencyId);

            var car = new Domain.Entities.Car
            {
                Matricule = request.Matricule.Trim(),
                ModelId = request.ModelId,
                BranchId = request.BranchId,
                // A car added while setting the agency up is meant to be sold:
                // anything else is a state the agency changes later.
                Status = CarStatus.Active,
            };

            // Denominated in the agency's currency, like every stored amount —
            // the client sends only the figure.
            if (request.DailyRate is decimal rate)
            {
                var currency = (await _settings.GetAsync(request.AgencyId, cancellationToken)).CurrencyCode;
                car.DailyRate = Money.Of(rate, currency);
            }

            // Quota check and insert are atomic under the per-agency write lock,
            // exactly as the agency's own create path does it.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                _context, _tenant, _dateTime, _context.Cars, p => p.MaxCars, "cars", cancellationToken);

            _context.Cars.Add(car);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return car.Id;
        }
    }
}

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyCarCommand
{
    public class CreateAgencyCarCommandValidator : AbstractValidator<CreateAgencyCarCommand>
    {
        public CreateAgencyCarCommandValidator()
        {
            RuleFor(v => v.AgencyId).GreaterThan(0);
            RuleFor(v => v.Matricule).NotEmpty().MaximumLength(50);
            RuleFor(v => v.DailyRate).GreaterThan(0m).When(v => v.DailyRate is not null);
        }
    }
}
