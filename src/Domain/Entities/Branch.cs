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

        // The street address, as shown to customers looking for the pick-up
        // place. Usually the reverse-geocoded label of Location — the two are
        // set together when the address is picked on a map — but free text, so
        // it can be corrected by hand where the geocoder is wrong or vague.
        public string? Address { get; set; }

        // Geography point (SRID 4326). The branch is the geographic anchor of
        // an agency (the agency itself keeps only an HQ address); nullable
        // until geocoded, queried with Distance/IsWithinDistance for
        // "nearby" search.
        public Point? Location { get; set; }
    }
}
