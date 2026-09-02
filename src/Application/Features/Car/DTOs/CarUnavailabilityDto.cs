using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    /// <summary>
    /// One stretch of dates a car is off the road. <see cref="EndDate"/> is
    /// exclusive, like every period in the booking model — the screen renders it
    /// as "back on the 24th", not "away until the 24th".
    /// </summary>
    public class CarUnavailabilityDto
    {
        public int Id { get; init; }
        public int CarId { get; init; }
        public string? CarMatricule { get; init; }

        /// <summary>Wall-clock: render with the 'UTC' date argument.</summary>
        public DateTime StartDate { get; init; }

        /// <summary>Wall-clock and exclusive; also render with 'UTC'.</summary>
        public DateTime EndDate { get; init; }

        public CarUnavailabilityReason Reason { get; init; }
        public string? Note { get; init; }

        /// <summary>
        /// The block covers today, so the car is away right now. Computed on the
        /// server: the client's clock may disagree about which day it is, and
        /// that decides whether a row reads as current or as upcoming.
        /// </summary>
        public bool IsCurrent { get; init; }
    }
}
