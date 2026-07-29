namespace RemSolution.Application.Features.MarketplaceSearch.DTOs
{
    // One published review on an agency's shopfront. Public, so it carries the
    // author's display name only — never their e-mail, phone or client id.
    public class AgencyReviewDto
    {
        public int Id { get; init; }
        public int Rating { get; init; }
        public string? Comment { get; init; }
        public string? AuthorName { get; init; }
        public DateTime SubmittedAt { get; init; }
        // What was rented, so a reader can see the review is about a real rental.
        // Snapshotted on the review row (see AgencyReview.CarName) — the Car and
        // Renting tables are tenant-filtered and unreadable to a visitor.
        public string? CarName { get; init; }
    }

    // An agency's reputation at a glance: the average, how many people it is
    // based on, and how the stars are spread. The breakdown matters — a 4.0 made
    // of 4s reads very differently from one made of 5s and 1s.
    public class AgencyRatingSummaryDto
    {
        public double? AverageRating { get; init; }
        public int ReviewCount { get; init; }
        // Index 0 = one star … index 4 = five stars.
        public IList<int> Counts { get; init; } = new List<int>();
    }
}
