using NetTopologySuite.Geometries;

namespace RemSolution.Domain.Entities
{
    public class Branch : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public string? Name { get; set; }
        public int CountryId { get; set; }
        public virtual Country? Country { get; set; }

        // Geography point (SRID 4326). The branch is the geographic anchor of
        // an agency (the agency itself keeps only an HQ address); nullable
        // until geocoded, queried with Distance/IsWithinDistance for
        // "nearby" search.
        public Point? Location { get; set; }
    }
}
