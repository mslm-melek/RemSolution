using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;
using RentingHistoryEntity = RemSolution.Domain.Entities.RentingHistory;

namespace RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand
{
    // The forward lifecycle transition: NotYet → InProgress (pickup) →
    // Done (return). Cancellation has its own command (CancelRentingCommand).
    // Auditable: the state change is the renting's history of custody, so it is
    // recorded on the audit trail (P.2 — no separate status-history table).
    [Authorize(Policy = Permissions.RentingUpdate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("ChangeRentingState", "Renting")]
    public record ChangeRentingStateCommand : IRequest
    {
        public int Id { get; init; }
        public byte[]? RowVersion { get; init; }
        public RentingState NewState { get; init; }
        // Odometer reading captured at the transition: StartMileage on pickup,
        // EndMileage on return.
        public int? Mileage { get; init; }

        /// <summary>
        /// Extra charges established as the car came back — a late day, a dent,
        /// the kilometres over the allowance (see Renting.AddFee). They ride on
        /// the return rather than following it as separate calls so that closing
        /// a hire is one write: the counter never ends up with a hire marked
        /// returned and the damage un-billed because the second call failed.
        /// <para>Only on the Done transition; anything else is refused.</para>
        /// </summary>
        public IList<RentingFeePayload>? Fees { get; init; }

        /// <summary>
        /// What becomes of the deposit, decided at the counter with the car in
        /// front of the person deciding. Asked here because this is the only
        /// moment the answer is obvious — chased afterwards it becomes a
        /// three-week-old argument (see Renting.SettleDeposit). Omitting it
        /// leaves the deposit unsettled rather than assuming a refund, so it
        /// stays on the desk's list.
        /// <para>Only on the Done transition; anything else is refused.</para>
        /// </summary>
        public DepositSettlementPayload? DepositSettlement { get; init; }
    }

    public class ChangeRentingStateCommandHandler : IRequestHandler<ChangeRentingStateCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public ChangeRentingStateCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task Handle(ChangeRentingStateCommand request, CancellationToken cancellationToken)
        {
            // Fees are added through the aggregate, which needs its own list in
            // hand to add to; a pickup carries none, so the join is only paid for
            // on a return that books some.
            IQueryable<Domain.Entities.Renting> rentings = _context.Rentings;

            if (request.Fees is { Count: > 0 })
            {
                rentings = rentings.Include(r => r.Fees);
            }

            var entity = await rentings
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            switch (request.NewState)
            {
                case RentingState.InProgress:
                    entity.Start(request.Mileage);
                    break;

                case RentingState.Done:
                    entity.Complete(request.Mileage, _dateTime.GetUtcNow().UtcDateTime);

                    // What the return turned up, booked in the same unit of work
                    // as the return itself. After Complete, because the aggregate
                    // only takes fees on a car that went out or came back.
                    if (request.Fees is { Count: > 0 })
                    {
                        var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

                        RentingFees.AddAll(
                            entity, request.Fees, RentingFees.CurrencyOf(entity, settings.CurrencyCode));
                    }

                    // The deposit, in the same write as the return: the car is
                    // there, the damage is visible, and the money is decided once.
                    if (request.DepositSettlement is { } settlement)
                    {
                        if (!entity.HasUnsettledDeposit)
                        {
                            throw new ValidationException(new[]
                            {
                                new ValidationFailure(nameof(request.DepositSettlement),
                                    entity.DepositAmount is null or { Amount: <= 0m }
                                        ? "This hire took no deposit."
                                        : "This hire's deposit has already been settled.")
                            });
                        }

                        var refund = DepositSettlements.Settle(
                            entity, settlement, _dateTime.GetUtcNow().UtcDateTime);

                        if (refund is not null)
                        {
                            _context.Payments.Add(refund);
                        }
                    }

                    // Snapshot the finished period. Written here (not in the event
                    // handler) so it goes through the tenant/audit interceptors in
                    // the same save. AgencyId is set explicitly as well.
                    _context.RentingHistories.Add(new RentingHistoryEntity
                    {
                        AgencyId = entity.AgencyId,
                        RentingId = entity.Id,
                        StartDate = entity.StartDate,
                        EndDate = entity.EndDate,
                        StartMileage = entity.StartMileage,
                        EndMileage = entity.EndMileage,
                        Price = entity.Price,
                        RentingState = RentingState.Done,
                    });
                    break;

                default:
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.NewState),
                            $"'{request.NewState}' is not a valid forward transition. " +
                            "Use InProgress or Done, or cancel the renting.")
                    });
            }

            // Pickup and return readings are both measured on the car, so the
            // car's own odometer follows them (see Car.RecordOdometer). Loaded
            // only when there is a reading to record, and saved in the same unit
            // of work as the transition: the two cannot disagree afterwards.
            if (request.Mileage.HasValue)
            {
                var car = await _context.Cars
                    .FirstOrDefaultAsync(c => c.Id == entity.CarId, cancellationToken);

                car?.RecordOdometer(request.Mileage);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand
{
    public class ChangeRentingStateCommandValidator : AbstractValidator<ChangeRentingStateCommand>
    {
        public ChangeRentingStateCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.NewState).IsInEnum();
            RuleFor(v => v.Mileage).GreaterThanOrEqualTo(0).When(v => v.Mileage.HasValue);

            RuleForEach(v => v.Fees).SetValidator(new RentingFeePayloadValidator());

            // Extra charges are things found on a returning car, so a pickup
            // carrying them is a mistake on the caller's side, not a silent
            // no-op that leaves the money unbilled.
            RuleFor(v => v.Fees)
                .Empty()
                .When(v => v.NewState != RentingState.Done)
                .WithMessage("Extra charges can only be recorded when the hire is returned.");

            RuleFor(v => v.DepositSettlement!)
                .SetValidator(new DepositSettlementPayloadValidator())
                .When(v => v.DepositSettlement is not null);

            // Same reason as the fees above: the deposit is decided when the car
            // comes back, so settling it on a pickup is a caller mistake.
            RuleFor(v => v.DepositSettlement)
                .Null()
                .When(v => v.NewState != RentingState.Done)
                .WithMessage("The deposit can only be settled when the hire is returned.");
        }
    }
}
