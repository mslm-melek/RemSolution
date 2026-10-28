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

        /// <summary>The verdict's reasoning — or, when Withdrawn, the customer's reason.</summary>
        public string? ResolutionNote { get; init; }

        public DateTime? ReopenedAt { get; init; }

        /// <summary>
        /// Whether the customer left an address to be mailed the platform's replies
        /// and verdict. Platform screen only — the address itself is never sent,
        /// and the agency's list leaves this false.
        /// </summary>
        public bool CustomerGetsEmail { get; init; }

        /// <summary>
        /// Who wrote the last message on the thread — false means the customer
        /// did and the platform owes an answer. Null when nobody has written one
        /// (withdrawals and reopenings do not count; see AgencyReportThreads).
        /// </summary>
        public bool? LastMessageFromPlatform { get; set; }

        /// <summary>The customer ↔ platform thread, oldest first.</summary>
        // Settable: filled after the page is read (see AgencyReportThreads).
        public IList<AgencyReportMessageDto> Messages { get; set; } = new List<AgencyReportMessageDto>();
    }
}
