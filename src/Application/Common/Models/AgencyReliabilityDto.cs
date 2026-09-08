using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Common.Models
{
    /// <summary>
    /// An agency's reliability, as the marketplace shows it and as the agency
    /// sees it about itself. The counts travel with the score because the score
    /// alone is not accountable: an agency told it has 70 has to be able to see
    /// which bookings that is.
    /// </summary>
    public class AgencyReliabilityDto
    {
        /// <summary>Bookings the agency confirmed, whatever became of them afterwards.</summary>
        public int ConfirmedBookings { get; init; }

        /// <summary>Of those, the ones the agency itself then called off.</summary>
        public int CancelledByAgency { get; init; }

        /// <summary>Complaints a platform administrator found justified.</summary>
        public int UpheldReports { get; init; }

        public int Honoured { get; init; }

        /// <summary>Null when the agency has no record yet — see <see cref="AgencyReliability"/>.</summary>
        public int? Score { get; init; }

        public static AgencyReliabilityDto From(AgencyReliability reliability) => new()
        {
            ConfirmedBookings = reliability.ConfirmedBookings,
            CancelledByAgency = reliability.CancelledByAgency,
            UpheldReports = reliability.UpheldReports,
            Honoured = reliability.Honoured,
            Score = reliability.Score,
        };
    }
}
