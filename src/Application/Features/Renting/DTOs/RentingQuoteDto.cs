using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.DTOs
{
    /// <summary>
    /// What a car would cost for a period, and whether it can be booked for it.
    /// Produced by GetRentingQuoteQuery for the booking screen; nothing is stored.
    /// </summary>
    public class RentingQuoteDto
    {
        public int CarId { get; init; }

        /// <summary>The car's current rate; null when it has none set.</summary>
        public MoneyDto? DailyRate { get; init; }

        /// <summary>Days charged for the period — a started day counts in full.</summary>
        public int BilledDays { get; init; }

        /// <summary>
        /// Automatic price for the whole period. Null when the car has no rate (or
        /// the period is not yet valid), which is when a manual price is needed.
        /// </summary>
        public MoneyDto? Price { get; init; }

        /// <summary>
        /// Currency any manual price is taken to be in — the car's, falling back to
        /// the agency's. Always set, even when there is no automatic price.
        /// </summary>
        public string Currency { get; init; } = string.Empty;

        public CarStatus CarStatus { get; init; }

        /// <summary>False for a car that is not Active; it cannot be booked at all.</summary>
        public bool IsCarBookable { get; init; }

        /// <summary>
        /// Whether the car can be booked for this period: false when an active
        /// renting or reservation already overlaps it, and also false when there is
        /// no period to check yet (<see cref="BilledDays"/> is 0) — so a caller can
        /// treat it as "this is not bookable as it stands" without a second test.
        /// Advisory: the create/update handlers re-check under the write lock.
        /// </summary>
        public bool IsAvailable { get; init; }
    }
}
