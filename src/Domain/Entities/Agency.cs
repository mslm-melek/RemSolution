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
        // Public customer ratings. Platform-level like the agency itself (see
        // AgencyReview), so this navigation is safe to project from anonymous
        // marketplace queries without any query-filter bypass.
        public virtual ICollection<AgencyReview>? Reviews { get; set; }
    }
}
