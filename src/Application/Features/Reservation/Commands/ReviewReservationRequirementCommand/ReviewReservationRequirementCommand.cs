using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Reservation.Commands.ReviewReservationRequirementCommand
{
    /// <summary>What the agency decided about one requirement.</summary>
    public enum ReservationRequirementDecision
    {
        Accept = 1,
        Reject = 2,
        Waive = 3,
    }

    // The agency looks at what the customer sent — or ticks off what was handed
    // over at the counter. The guards live in the aggregate; a rejection without
    // a reason is refused there, because the customer has to be told what to send
    // instead.
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("ReviewReservationRequirement", "Reservation")]
    public record ReviewReservationRequirementCommand : IRequest
    {
        public int Id { get; init; }
        public ReservationRequirementDecision Decision { get; init; }
        public string? Note { get; init; }
    }

    public class ReviewReservationRequirementCommandHandler
        : IRequestHandler<ReviewReservationRequirementCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public ReviewReservationRequirementCommandHandler(
            IApplicationDbContext context, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task Handle(
            ReviewReservationRequirementCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ReservationRequirements
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            var now = _dateTime.GetUtcNow().UtcDateTime;

            switch (request.Decision)
            {
                case ReservationRequirementDecision.Accept:
                    entity.Accept(now, _user.Id, request.Note);
                    break;
                case ReservationRequirementDecision.Reject:
                    entity.Reject(now, _user.Id, request.Note ?? string.Empty);
                    break;
                case ReservationRequirementDecision.Waive:
                    entity.Waive(now, _user.Id, request.Note);
                    break;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.ReviewReservationRequirementCommand
{
    public class ReviewReservationRequirementCommandValidator
        : AbstractValidator<ReviewReservationRequirementCommand>
    {
        public ReviewReservationRequirementCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Decision).IsInEnum();
            RuleFor(v => v.Note)
                .MaximumLength(Domain.Entities.ReservationRequirement.MaxNoteLength);

            RuleFor(v => v.Note)
                .NotEmpty()
                .When(v => v.Decision == ReservationRequirementDecision.Reject)
                .WithMessage("Say why it was refused.");
        }
    }
}
