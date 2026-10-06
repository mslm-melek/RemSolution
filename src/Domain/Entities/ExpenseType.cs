namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One agency's expense type: either its own, or its copy of a platform
    /// <see cref="ExpenseTypeTemplate"/> (<see cref="TemplateId"/> set).
    /// </summary>
    public class ExpenseType : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        /// <summary>The template's translation key, or the agency's own words; see <see cref="CatalogName"/>.</summary>
        public string? Name { get; set; }
        // Deactivation, not deletion: an inactive type is hidden from new-entry
        // pickers but kept so historical expenses still resolve their type.
        public bool IsActive { get; set; } = true;
        public bool WithNotif { get; set; }
        public int? AfterKilometer { get; set; }
        public int? AfterMonth { get; set; }

        /// <summary>The platform template this was copied from; null for the agency's own.</summary>
        public int? TemplateId { get; set; }
        public virtual ExpenseTypeTemplate? Template { get; set; }

        /// <summary>
        /// Set once the agency changes anything the template owns (name,
        /// schedule); from then on the platform's edits no longer reach this copy.
        /// Switching the type on or off is the agency's own choice and does not count.
        /// </summary>
        public bool IsCustomized { get; set; }

        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

        /// <summary>
        /// Back to the platform's version: the template's name and schedule, and
        /// its later edits reach this copy again. Whether it is switched on stays
        /// the agency's choice.
        /// </summary>
        public void ResetTo(ExpenseTypeTemplate template)
        {
            Name = template.Name;
            WithNotif = template.WithNotif;
            AfterKilometer = template.AfterKilometer;
            AfterMonth = template.AfterMonth;
            IsCustomized = false;
        }

        public void Describe(string name, bool withNotif, int? afterKilometer, int? afterMonth)
        {
            var changed = Name != name
                || WithNotif != withNotif || AfterKilometer != afterKilometer || AfterMonth != afterMonth;

            if (changed && TemplateId is not null)
            {
                IsCustomized = true;
            }

            Name = name;
            WithNotif = withNotif;
            AfterKilometer = afterKilometer;
            AfterMonth = afterMonth;
        }
    }
}
