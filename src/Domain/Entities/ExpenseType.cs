namespace RemSolution.Domain.Entities
{
    public class ExpenseType : BaseAuditableEntity
    {
        public string? Name { get; set; }
        // Deactivation, not deletion: an inactive type is hidden from new-entry
        // pickers but kept so historical expenses still resolve their type.
        public bool IsActive { get; set; } = true;
        public bool WithNotif { get; set; }
        public int? AfterKilometer { get; set; }
        public int? AfterMonth { get; set; }
        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
