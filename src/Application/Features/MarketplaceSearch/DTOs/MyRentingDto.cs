using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // One of the signed-in customer's rentals, across every agency they have
    // rented from. This is the "my trips" list, and it is where a finished rental
    // turns into a rating.
    public class MyRentingDto
    {
        public int RentingId { get; init; }
        public int AgencyId { get; init; }
        public string? AgencyName { get; init; }
        public string? CarBrandName { get; init; }
        public string? CarModelName { get; init; }
        public string? CarImageUrl { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public RentingState RentingState { get; init; }
        public MoneyDto? Price { get; init; }

        // True when the rental is finished and not yet rated — the single
        // condition the "Rate this rental" button is bound to, decided by the
        // server so the rule lives in one place.
        public bool CanReview { get; init; }
        // The rating already left, if any. Present so the page can show it back
        // instead of offering the button again.
        public int? MyRating { get; init; }
        public string? MyComment { get; init; }
        public DateTime? ReviewedAt { get; init; }
    }
}
