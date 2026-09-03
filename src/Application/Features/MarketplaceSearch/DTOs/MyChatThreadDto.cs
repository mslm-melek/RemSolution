using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // A customer's conversation about one of their bookings, viewed across
    // agencies. The mirror of ChatThreadDto: it names the agency rather than the
    // client, and its unread count is the agency's messages the customer has not
    // opened yet.
    public class MyChatThreadDto
    {
        // The booking the thread hangs off — a hire, or a hold the agency has
        // confirmed. Exactly one of the two ids is set.
        public ChatSubjectKind Subject { get; init; }
        public int? RentingId { get; init; }
        public int? ReservationId { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public string? CarBrandName { get; init; }
        public string? CarModelName { get; init; }
        public string? CarMatricule { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public RentingState? RentingState { get; init; }
        public ReservationStatus? ReservationStatus { get; init; }
        public string? LastMessagePreview { get; init; }
        public DateTime? LastMessageAt { get; init; }
        public ChatAuthorKind? LastMessageAuthorKind { get; init; }
        public int UnreadCount { get; init; }
        // Whether the customer may still post — a closed booking is read-only.
        public bool IsOpen { get; init; }

        public int SubjectId => (Subject == ChatSubjectKind.Reservation ? ReservationId : RentingId) ?? 0;
    }
}
