using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // An agency's public shopfront: who they are, where you can pick a car up
    // and what they charge. Contact details are the agency's business contact —
    // the point of a listing is that a customer can reach them.
    public class MarketplaceAgencyDto
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? CountryName { get; init; }
        public string? Address { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Email { get; init; }
        // Cars the agency offers on the marketplace (active and priced),
        // independent of any date window — the "cars for these dates" list is
        // the search query, which the agency page runs separately.
        public int CarCount { get; init; }
        // Cheapest offered daily rate, for the "from X / day" line.
        public MoneyDto? FromDailyRate { get; init; }
        // Public reputation. Null average = never reviewed, which the page says
        // in words rather than rendering as zero stars.
        public AgencyRatingSummaryDto Rating { get; init; } = new();
        // How often the agency has kept its side. Deliberately on the shopfront
        // only and not on every car card: the denominator is a count over the
        // agency's bookings, which as a correlated sub-query per card would make
        // a page of search results pay for it.
        public AgencyReliabilityDto Reliability { get; init; } = new();
        public IList<MarketplacePlaceDto> Places { get; init; } = new List<MarketplacePlaceDto>();
    }
}
