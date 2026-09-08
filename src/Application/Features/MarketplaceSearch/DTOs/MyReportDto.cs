using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // A complaint the signed-in customer raised, and what became of it. The
    // resolution note is here because the outcome is owed to them: a report that
    // disappears into the platform is worse than no report at all.
    public class MyReportDto
    {
        public int Id { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public int? ReservationId { get; init; }
        public int? RentingId { get; init; }
        public string? BookingSummary { get; init; }
        public AgencyReportKind Kind { get; init; }
        public string? Message { get; init; }
        public AgencyReportStatus Status { get; init; }
        // Instants — the SPA renders them local.
        public DateTime SubmittedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
        public string? ResolutionNote { get; init; }
    }
}
