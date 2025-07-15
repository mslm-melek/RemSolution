namespace RemSolution.Domain.Entities
{
    public class ExpenseType : BaseAuditableEntity
    {
        public string? Name { get; set; }
        public bool WithNotif { get; set; }
        public int? AfterKilometer { get; set; }
        public int? AfterMonth { get; set; }
        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
