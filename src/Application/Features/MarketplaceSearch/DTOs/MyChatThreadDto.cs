using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // A customer's conversation about one of their rentings, viewed across
    // agencies. The mirror of ChatThreadDto: it names the agency rather than the
    // client, and its unread count is the agency's messages the customer has not
    // opened yet.
    public class MyChatThreadDto
    {
        public int RentingId { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public string? CarBrandName { get; init; }
        public string? CarModelName { get; init; }
        public string? CarMatricule { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public RentingState RentingState { get; init; }
        public string? LastMessagePreview { get; init; }
        public DateTime? LastMessageAt { get; init; }
        public ChatAuthorKind? LastMessageAuthorKind { get; init; }
        public int UnreadCount { get; init; }
        // Whether the customer may still post — closed rentings are read-only.
        public bool IsOpen { get; init; }
    }
}
