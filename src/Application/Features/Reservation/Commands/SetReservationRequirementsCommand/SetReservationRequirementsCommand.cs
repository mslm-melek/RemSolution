using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Reservation.Commands.SetReservationRequirementsCommand
{
    /// <summary>One thing to ask the customer for.</summary>
    public record ReservationRequirementItem
    {
        /// <summary>Zero for a new ask; an existing id keeps its answer and its history.</summary>
        public int Id { get; init; }
        public ReservationRequirementKind Kind { get; init; }
        public string Label { get; init; } = string.Empty;
        // Amount only — the currency is the agency's (see the Money convention).
        public decimal? Amount { get; init; }
        public PaymentMethod? ExpectedMethod { get; init; }
    }

    // What the agency wants before the keys change hands, set on a hold it has
    // already committed to. Sending the full list replaces it: an ask that is
    // dropped from the list is REMOVED when nobody has answered it yet, and
    // waived when someone has, so the customer's answer is never deleted out
    // from under the record of what was asked.
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("SetReservationRequirements", "Reservation")]
    public record SetReservationRequirementsCommand : IRequest
    {
        public int Id { get; init; }
        public IList<ReservationRequirementItem> Items { get; init; } = new List<ReservationRequirementItem>();
    }

    public class SetReservationRequirementsCommandHandler
        : IRequestHandler<SetReservationRequirementsCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public SetReservationRequirementsCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings,
            IUser user, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task Handle(
            SetReservationRequirementsCommand request, CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Requirements)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, reservation);

            // Only on a hold the agency has committed to: asking a customer for
            // their papers before answering their request is asking them to work
            // for an answer that may never come.
            if (reservation.Status is not (ReservationStatus.Confirmed or ReservationStatus.Paid))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "Requirements can only be set on a confirmed reservation.")
                });
            }

            var settings = await _settings.GetAsync(reservation.AgencyId, cancellationToken);
            var now = _dateTime.GetUtcNow().UtcDateTime;
            var existing = reservation.Requirements?.ToList() ?? new List<ReservationRequirement>();
            var kept = new HashSet<int>();

            foreach (var item in request.Items)
            {
                var amount = item.Amount is decimal value
                    ? Money.Of(value, settings.CurrencyCode)
                    : null;

                if (item.Id > 0)
                {
                    var current = existing.FirstOrDefault(r => r.Id == item.Id);

                    Guard.Against.NotFound(item.Id, current);

                    kept.Add(current.Id);

                    // Only an unanswered ask can be re-worded; the aggregate says
                    // so, and re-pricing something already paid would rewrite
                    // history the customer acted on.
                    if (current.Status == ReservationRequirementStatus.Requested)
                    {
                        current.Amend(item.Label, amount, item.ExpectedMethod);
                    }

                    continue;
                }

                _context.ReservationRequirements.Add(ReservationRequirement.Create(
                    reservation.Id, item.Kind, item.Label, amount, item.ExpectedMethod));
            }

            foreach (var dropped in existing.Where(r => !kept.Contains(r.Id)))
            {
                if (dropped.Status == ReservationRequirementStatus.Requested)
                {
                    _context.ReservationRequirements.Remove(dropped);
                }
                else
                {
                    // Answered, then dropped: waived rather than deleted, so the
                    // customer's file and the fact it was once required survive.
                    dropped.Waive(now, _user.Id, "No longer required.");
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.SetReservationRequirementsCommand
{
    public class SetReservationRequirementsCommandValidator
        : AbstractValidator<SetReservationRequirementsCommand>
    {
        public SetReservationRequirementsCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);

            RuleForEach(v => v.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.Label)
                    .NotEmpty()
                    .MaximumLength(ReservationRequirement.MaxLabelLength);

                item.RuleFor(i => i.Amount)
                    .GreaterThanOrEqualTo(0).When(i => i.Amount.HasValue);

                item.RuleFor(i => i.Kind).IsInEnum();
            });
        }
    }
}
