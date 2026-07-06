namespace RemSolution.Domain.Entities
{
    public class Reservation : BaseAuditableEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Price { get; set; }
        public decimal? PayedPrice { get; set; }
        public string? Notes { get; set; }
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public RentingState? RentingState { get; set; }

    }
}
