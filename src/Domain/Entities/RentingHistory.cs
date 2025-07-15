namespace RemSolution.Domain.Entities
{
    public class RentingHistory : BaseAuditableEntity
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? StartMileage { get; set; }
        public int? EndMileage { get; set; }
        public decimal? Price { get; set; }
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public RentingState RentingState { get; set; }

    }
}
