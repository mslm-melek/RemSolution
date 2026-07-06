namespace RemSolution.Domain.Entities
{
    public class Expense : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int CarId { get; set; }
        public virtual Car? Car { get; set; }
        public int ExpenseTypeId { get; set; }
        public virtual ExpenseType? ExpenseType { get; set; }
        public DateTime ExpenseDate { get; set; } = DateTime.Now;
        public decimal? ExpenseAmount { get; set; }
        public string? FactureImageUrl { get; set; }
        public string? Description { get; set; }


    }
}
