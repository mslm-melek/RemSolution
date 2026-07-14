namespace RemSolution.Domain.Entities
{
    public class Agency : BaseAuditableEntity
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        // HQ address only — the geographic anchor for spatial queries is the
        // agency's branches (Branch.Location), not the agency itself.
        public string? Address { get; set; }
        public int CountryId { get; set; }
        public virtual Country? Country { get; set; }
    }
}
