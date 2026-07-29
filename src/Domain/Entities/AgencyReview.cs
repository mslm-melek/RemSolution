namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A customer's rating of an agency, written once per finished renting.
    /// The renting is the proof of purchase: there is no free-floating review,
    /// so a rating always comes from someone who actually rented the car.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT an <c>ITenantEntity</c>. A review is public content —
    /// every visitor of the marketplace reads it, and every car card carries its
    /// agency's average — so it lives at platform level next to
    /// <see cref="Agency"/> itself. Were it tenant-scoped, the public queries
    /// would each need an IgnoreQueryFilters bypass and the rating could not be
    /// projected through the <c>Agency.Reviews</c> navigation.
    ///
    /// The trade-off is that agency-facing code must filter by
    /// <see cref="AgencyId"/> explicitly; there is no global filter to lean on.
    /// </remarks>
    public class AgencyReview : BaseAuditableEntity
    {
        public const int MinRating = 1;
        public const int MaxRating = 5;
        public const int MaxCommentLength = 2000;

        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        // The renting being reviewed. Unique: one rating per rental, so an
        // agency cannot be starred twice off the same booking.
        public int RentingId { get; set; }
        public virtual Renting? Renting { get; set; }

        // The agency's Client row for this customer, kept so the agency can tell
        // who wrote a review without joining back through the renting.
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }

        // Identity user id of the marketplace account that wrote it — the same
        // link (Client.MarketplaceUserId) that proves ownership on write.
        public string? AuthorUserId { get; set; }

        // Display name snapshotted at submit time, so the review stays readable
        // after the client record is renamed or archived.
        public string? AuthorName { get; set; }

        // What was rented ("Renault Clio"), snapshotted for the same reason the
        // author's name is — but also because the public shopfront must render a
        // review without touching Renting or Car, which are tenant-filtered and
        // would come back empty for an anonymous visitor.
        public string? CarName { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }

        // UTC, like every domain DateTime (enforced at the persistence boundary).
        public DateTime SubmittedAt { get; set; }

        /// <summary>
        /// Whether a renting in this state can be reviewed. Only a finished
        /// rental can: rating a car you have not returned yet is not an opinion
        /// about the service, and a cancelled rental never happened.
        /// </summary>
        /// <remarks>
        /// The "my rentals" projection cannot call this — EF has to translate the
        /// test into SQL — so it repeats the same state inline as
        /// <c>CanReview</c>. Change this rule and that projection changes with it.
        /// </remarks>
        public static bool CanReview(RentingState state) => state == RentingState.Done;

        public static bool IsValidRating(int rating) => rating is >= MinRating and <= MaxRating;
    }
}
