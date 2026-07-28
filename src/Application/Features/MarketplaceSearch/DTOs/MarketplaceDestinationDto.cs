namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // A country a visitor can pick on the public search bar, with the places
    // (branches) inside it. Built from the cars actually offered on the
    // marketplace, so the picker never lists a destination with nothing in it.
    public class MarketplaceDestinationDto
    {
        public int CountryId { get; init; }
        public string? CountryName { get; init; }
        // Offered cars in the country — the branch-anchored ones plus any car
        // whose agency sits here but that has no branch of its own.
        public int CarCount { get; init; }
        public IList<MarketplacePlaceDto> Places { get; init; } = new List<MarketplacePlaceDto>();
    }

    // A pick-up place: an agency branch. A car with no branch is counted in its
    // country's total but belongs to no place, so it is only reachable by
    // searching the country (or the agency) rather than a place.
    public class MarketplacePlaceDto
    {
        public int BranchId { get; init; }
        public string? Name { get; init; }
        public string? AgencyName { get; init; }
        public int AgencyId { get; init; }
        public int CarCount { get; init; }
    }
}
