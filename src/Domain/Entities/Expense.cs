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
        // UTC per the persistence-boundary rule (see docs "Time"); never local.
        // When the Expense feature slice is built its handler should stamp this
        // from an injected TimeProvider (as CreateCarCommand does).
        public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
        // Denominated in the owning agency's currency, like every other amount
        // (see Money). Null when the expense has no recorded amount.
        public Money? ExpenseAmount { get; set; }
        // Facture image as a StoredFile FK for schema consistency. The Expense
        // feature slice (and its upload flow) is not built yet, so nothing
        // populates this today — it is the deferred half of the StoredFile work.
        public int? FactureFileId { get; set; }
        public virtual StoredFile? FactureFile { get; set; }
        public string? Description { get; set; }


    }
}
