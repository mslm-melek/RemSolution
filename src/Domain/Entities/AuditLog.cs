namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// Business audit trail for sensitive actions (deletes, price changes,
    /// payments, subscription changes). One row per changed entity: who did it,
    /// when, in which agency, the action and entity, and the before/after state
    /// as JSON. This is what answers "who changed my data" when an agency
    /// disputes something.
    ///
    /// Platform-level, not an <c>ITenantEntity</c>: rows are written by the
    /// audit interceptor (never by handlers) and are never tenant-filtered, so
    /// platform admins can read across agencies. Append-only. The AgencyId is a
    /// plain column, captured from the acting tenant (null for platform-admin
    /// actions), not a tenant query-filter key.
    /// </summary>
    public class AuditLog : BaseEntity
    {
        /// <summary>Who: the acting user's id, or null for system/anonymous actions.</summary>
        public string? UserId { get; set; }

        /// <summary>
        /// The acting user's name at the time of the action — a snapshot, so the
        /// trail stays readable even if the account is later renamed or deleted.
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>In which agency the action happened; null for platform-admin actions.</summary>
        public int? AgencyId { get; set; }

        /// <summary>The business action, declared by the command's [Auditable] marker (e.g. "DeleteAgency").</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>The affected entity type (e.g. "Agency").</summary>
        public string Entity { get; set; } = string.Empty;

        /// <summary>The affected row's key, when known (empty for not-yet-persisted inserts).</summary>
        public string? EntityId { get; set; }

        /// <summary>State before the change as JSON; null for inserts.</summary>
        public string? Before { get; set; }

        /// <summary>State after the change as JSON; null for deletes.</summary>
        public string? After { get; set; }

        /// <summary>Correlates this audit row with the structured logs of the same request.</summary>
        public string? CorrelationId { get; set; }

        public DateTimeOffset OccurredOn { get; set; }
    }
}
