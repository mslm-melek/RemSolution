using NetTopologySuite.Geometries;

namespace RemSolution.Domain.Entities
{
    public class Agency : BaseAuditableEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        // Per-agency configuration (currency, cancellation window, reservation
        // expiry, …) lives in its own 1:1 table rather than as columns here, so
        // adding a setting never ALTERs Agencies. Read via IAgencySettingsProvider.
        public virtual AgencySettings? Settings { get; set; }
        // HQ address, with an optional pin for it (SRID 4326) so the address can
        // be picked on a map and shown back on one. Only ever read for that
        // single agency, so unlike Branch.Location it carries no spatial index:
        // the anchor for "cars near me" is still the agency's branches, which is
        // where a customer actually collects a car.
        public string? Address { get; set; }
        public Point? Location { get; set; }
        public int CountryId { get; set; }
        public virtual Country? Country { get; set; }

        /// <summary>
        /// When the agency went live on the marketplace, or null while it is
        /// still being set up. Publication is EXPLICIT: an agency exists in the
        /// back-office from the moment it is created, but its cars reach the
        /// public search only once somebody says so — half a fleet and no prices
        /// is not a shop window.
        /// </summary>
        /// <remarks>
        /// Read by every public query through <c>MarketplaceCars.Offered</c>,
        /// and by the two places that repeat the offered rule for one car (the
        /// car detail lookup and the customer booking command). Adding a public
        /// surface means adding this gate to it.
        /// </remarks>
        public DateTime? PublishedAt { get; private set; }

        public bool IsPublished => PublishedAt is not null;

        /// <summary>
        /// Goes live. Idempotent, and it keeps the first date: the question the
        /// column answers is "since when has this agency been on the
        /// marketplace", which a second click must not move.
        /// </summary>
        public void Publish(DateTime at) => PublishedAt ??= at;

        /// <summary>
        /// Off the marketplace again. Existing bookings are untouched — they are
        /// between a customer and the agency, and a listing coming down does not
        /// cancel them.
        /// </summary>
        public void Unpublish() => PublishedAt = null;
        // Public customer ratings. Platform-level like the agency itself (see
        // AgencyReview), so this navigation is safe to project from anonymous
        // marketplace queries without any query-filter bypass.
        public virtual ICollection<AgencyReview>? Reviews { get; set; }
        // Complaints customers raised against this agency. Platform-level too,
        // and for a stronger reason than the reviews (see AgencyReport): nobody
        // who touches one carries a tenant claim.
        public virtual ICollection<AgencyReport>? Reports { get; set; }
    }
}
