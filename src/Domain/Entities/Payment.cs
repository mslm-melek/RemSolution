namespace RemSolution.Domain.Entities
{
    public class Payment : BaseAuditableEntity
    {
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public DateTime? PayementDate { get; set; }
        public decimal? PayementAmount { get; set; }

    }
}
