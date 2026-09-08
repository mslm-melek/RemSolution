using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencyReport.DTOs
{
    /// <summary>
    /// A complaint as the two sides that did not write it read it: the platform
    /// administrator arbitrating, and the agency answering for it.
    /// <para>
    /// Everything about the booking is a snapshot taken when the report was
    /// raised, not a join: the reader has no tenant claim, so the reservation
    /// behind it is out of reach (see <c>AgencyReport</c>).
    /// </para>
    /// </summary>
    public class AgencyReportDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }

        public int? ReservationId { get; init; }
        public int? RentingId { get; init; }

        public string? ReporterName { get; init; }
        public string? BookingSummary { get; init; }

        /// <summary>What the agency said when it cancelled, so both accounts are here.</summary>
        public string? AgencyCancellationReason { get; init; }

        public AgencyReportKind Kind { get; init; }
        public string? Message { get; init; }
        public AgencyReportStatus Status { get; init; }

        /// <summary>Instant, not a wall-clock date — the SPA renders it local.</summary>
        public DateTime SubmittedAt { get; init; }

        public DateTime? ResolvedAt { get; init; }
        public string? ResolutionNote { get; init; }
    }
}
