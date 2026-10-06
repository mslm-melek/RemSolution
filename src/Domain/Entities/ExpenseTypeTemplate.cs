namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// An expense type the platform offers every agency. Agencies never read this
    /// table: each one gets its own <see cref="ExpenseType"/> copy
    /// (<see cref="ExpenseType.TemplateId"/>), which it can switch off or retune.
    /// An edit here reaches the copies the agency has not changed
    /// (<see cref="ExpenseType.IsCustomized"/>); retiring a template only stops it
    /// being given to new agencies — an existing copy is the agency's to switch off.
    /// </summary>
    public class ExpenseTypeTemplate : BaseAuditableEntity
    {
        /// <summary>A translation key; see <see cref="CatalogName"/>.</summary>
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool WithNotif { get; set; }
        public int? AfterKilometer { get; set; }
        public int? AfterMonth { get; set; }
    }
}
