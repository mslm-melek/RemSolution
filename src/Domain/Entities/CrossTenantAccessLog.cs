namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// Audit trail for the platform-admin cross-tenant read path
    /// (ICrossTenantAccess): one row per audited access, written before any
    /// tenant data is exposed. Append-only; never tenant-filtered.
    /// </summary>
    public class CrossTenantAccessLog : BaseEntity
    {
        public string UserId { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public DateTimeOffset OccurredOn { get; set; }
    }
}
