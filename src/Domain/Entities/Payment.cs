namespace RemSolution.Domain.Entities
{
    public class Payment : BaseAuditableEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public DateTime? PayementDate { get; set; }
        public decimal? PayementAmount { get; set; }

    }
}
