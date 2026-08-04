using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Events;
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
    }

    public class ChangeRentingStateCommandHandler : IRequestHandler<ChangeRentingStateCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public ChangeRentingStateCommandHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task Handle(ChangeRentingStateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            switch (request.NewState)
            {
                case RentingState.InProgress:
                    Require(entity.RentingState == RentingState.NotYet,
                        "Only an upcoming (NotYet) renting can be started.");
                    if (request.Mileage.HasValue)
                    {
                        entity.StartMileage = request.Mileage;
                    }
                    entity.RentingState = RentingState.InProgress;
                    break;

                case RentingState.Done:
                    Require(entity.RentingState == RentingState.InProgress,
                        "Only an in-progress renting can be completed.");
                    if (request.Mileage.HasValue)
                    {
                        Require(!entity.StartMileage.HasValue || request.Mileage >= entity.StartMileage,
                            "The return mileage cannot be less than the pickup mileage.");
                        entity.EndMileage = request.Mileage;
                    }
                    entity.EndDate ??= _dateTime.GetUtcNow().UtcDateTime;
                    entity.RentingState = RentingState.Done;

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

                    // First consumer of the domain-event pipeline; side-effects
                    // (notifications, stats) hang off this event later.
                    entity.AddDomainEvent(new RentingCompletedEvent(entity));
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(ChangeRentingStateCommand.NewState), message)
                });
            }
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
        }
    }
}
