using NetTopologySuite.Geometries;

namespace RemSolution.Domain.Entities
{
    public class Agency : BaseAuditableEntity
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public int CountryId { get; set; }
        public virtual Country? Country { get; set; }

        // Geography point (SRID 4326). Nullable until the agency is geocoded;
        // queried with Distance/IsWithinDistance for "nearby" search.
        public Point? Location { get; set; }
    }
}
