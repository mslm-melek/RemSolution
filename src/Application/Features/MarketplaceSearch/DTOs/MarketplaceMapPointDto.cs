using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // One pin on the public map: a pick-up place with cars free for the searched
    // window. Grouped by branch rather than returned per car — several cars of
    // the same agency sit at the exact same coordinates, so per-car pins would
    // stack on top of each other and the cheapest one would be unreachable.
    public class MarketplaceMapPointDto
    {
        public int BranchId { get; init; }
        public string? BranchName { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        // Cars free for the searched window at this place — the number the pin
        // shows when it is opened, not the agency's whole fleet.
        public int CarCount { get; init; }
        // The label on the pin: what the cheapest of those cars costs per day.
        public MoneyDto? FromDailyRate { get; init; }
        public double? AgencyRating { get; init; }
        public int AgencyReviewCount { get; init; }
    }
}
