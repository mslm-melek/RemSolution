namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One agency's add-on type (GPS, child seat, …): either its own, or its copy
    /// of a platform <see cref="ExtraServicesTypeTemplate"/> (<see cref="TemplateId"/> set).
    /// </summary>
    public class ExtraServicesType : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        /// <summary>The template's translation key, or the agency's own words; see <see cref="CatalogName"/>.</summary>
        public string? Name { get; set; }

        /// <summary>
        /// The list price, in the agency's currency. Not part of the template:
        /// changing it never stops the platform's renames reaching this copy.
        /// </summary>
        public Money? Amount { get; set; }

        // Deactivation, not deletion: an inactive type is hidden from new-entry
        // pickers but kept so historical extra services still resolve their type.
        public bool IsActive { get; set; } = true;

        /// <summary>The platform template this was copied from; null for the agency's own.</summary>
        public int? TemplateId { get; set; }
        public virtual ExtraServicesTypeTemplate? Template { get; set; }

        /// <summary>Set once the agency renames a copy; see <see cref="ExpenseType.IsCustomized"/>.</summary>
        public bool IsCustomized { get; set; }

        public virtual ICollection<ExtraService> ExtraServices { get; set; } = new List<ExtraService>();

        /// <summary>
        /// Back to the platform's name, and its later renames reach this copy
        /// again. The price is the agency's own and stays: a template has none.
        /// </summary>
        public void ResetTo(ExtraServicesTypeTemplate template)
        {
            Name = template.Name;
            IsCustomized = false;
        }

        public void Rename(string name)
        {
            if (TemplateId is not null && Name != name)
            {
                IsCustomized = true;
            }

            Name = name;
        }
    }
}
