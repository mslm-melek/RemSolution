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
        // HQ address only — the geographic anchor for spatial queries is the
        // agency's branches (Branch.Location), not the agency itself.
        public string? Address { get; set; }
        public int CountryId { get; set; }
        public virtual Country? Country { get; set; }
        // Public customer ratings. Platform-level like the agency itself (see
        // AgencyReview), so this navigation is safe to project from anonymous
        // marketplace queries without any query-filter bypass.
        public virtual ICollection<AgencyReview>? Reviews { get; set; }
    }
}
