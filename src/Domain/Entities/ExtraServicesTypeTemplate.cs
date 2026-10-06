namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// An add-on (GPS, child seat, …) the platform offers every agency, copied
    /// into each one as an <see cref="ExtraServicesType"/> — same rules as
    /// <see cref="ExpenseTypeTemplate"/>. It carries no price: a price is in the
    /// agency's own currency, so each agency sets its own on its copy.
    /// </summary>
    public class ExtraServicesTypeTemplate : BaseAuditableEntity
    {
        /// <summary>A translation key; see <see cref="CatalogName"/>.</summary>
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
