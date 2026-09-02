using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A stretch of dates a car is off the road, declared in advance.
    /// <para>
    /// <see cref="Car.Status"/> answers "is this car available right now"; it
    /// cannot answer "is it available in three weeks", so nothing stopped a
    /// booking landing on the days the car is already booked into the garage.
    /// This row is the third thing the availability check consults, alongside
    /// hires and active holds — same half-open period, same overlap rule.
    /// </para>
    /// </summary>
    public class CarUnavailability : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int CarId { get; private set; }
        public virtual Car? Car { get; private set; }

        /// <summary>Wall-clock, inclusive of the day itself (see <see cref="EndDate"/>).</summary>
        public DateTime StartDate { get; private set; }

        /// <summary>
        /// Wall-clock and EXCLUSIVE, like every other period in the booking
        /// model: a car away "the 20th to the 24th" is unavailable from the 20th
        /// up to the 24th, and free to collect on the 24th. Storing it any other
        /// way would need the overlap predicate to special-case this table.
        /// </summary>
        public DateTime EndDate { get; private set; }

        public CarUnavailabilityReason Reason { get; private set; }

        public string? Note { get; private set; }

        public const int MaxNoteLength = 500;

        // EF materialisation; stored rows bypass the checks below.
        private CarUnavailability() { }

        public static CarUnavailability Create(
            int carId,
            DateTime startDate,
            DateTime endDate,
            CarUnavailabilityReason reason,
            string? note = null)
        {
            RequireCar(carId);
            RequirePeriod(startDate, endDate);
            RequireNote(note);

            return new CarUnavailability
            {
                CarId = carId,
                StartDate = startDate,
                EndDate = endDate,
                Reason = reason,
                Note = Trimmed(note),
            };
        }

        /// <summary>
        /// Moves or re-labels the block. The car is deliberately not editable:
        /// a block that belongs to another car is a different block, and moving
        /// it would skip the availability re-check the create path runs.
        /// </summary>
        public void Amend(DateTime startDate, DateTime endDate, CarUnavailabilityReason reason, string? note)
        {
            RequirePeriod(startDate, endDate);
            RequireNote(note);

            StartDate = startDate;
            EndDate = endDate;
            Reason = reason;
            Note = Trimmed(note);
        }

        /// <summary>Whether this block overlaps the half-open period [start, end).</summary>
        public bool Overlaps(DateTime start, DateTime end) => StartDate < end && EndDate > start;

        private static void RequireCar(int carId)
        {
            if (carId <= 0)
            {
                throw new DomainRuleException(nameof(CarId), "A block needs a car.");
            }
        }

        private static void RequirePeriod(DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate)
            {
                throw new DomainRuleException(nameof(EndDate),
                    "The end date must be after the start date.");
            }
        }

        private static void RequireNote(string? note)
        {
            if (note?.Length > MaxNoteLength)
            {
                throw new DomainRuleException(nameof(Note),
                    $"The note cannot be longer than {MaxNoteLength} characters.");
            }
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
