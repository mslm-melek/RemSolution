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
        // Facture image as a StoredFile FK for schema consistency. The Expense
        // feature slice (and its upload flow) is not built yet, so nothing
        // populates this today — it is the deferred half of the StoredFile work.
        public int? FactureFileId { get; set; }
        public virtual StoredFile? FactureFile { get; set; }
        public string? Description { get; set; }


    }
}
