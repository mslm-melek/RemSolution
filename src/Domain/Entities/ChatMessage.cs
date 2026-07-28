namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One message in the conversation attached to a renting. The renting IS the
    /// thread — there is no separate conversation row — so a message is always
    /// about a concrete booking and inherits its tenant, and the agency ⇄ client
    /// pair needs no membership table.
    /// </summary>
    public class ChatMessage : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        // The thread. Restrict on delete: a renting is a financial record and is
        // cancelled rather than removed, so its conversation stays readable.
        public int RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public ChatAuthorKind AuthorKind { get; set; }
        // Identity user id of whoever actually typed it — a staff member on the
        // agency side, the marketplace account on the client side.
        public string? SenderUserId { get; set; }
        // Display name snapshotted at send time so the thread stays readable
        // after a staff member is deactivated or renamed.
        public string? SenderName { get; set; }
        public string Body { get; set; } = string.Empty;
        // UTC, like every domain DateTime (enforced at the persistence boundary).
        public DateTime SentAt { get; set; }
        // When the OTHER side read this message; null means still unread. Read
        // state lives per message rather than as a per-thread cursor so an unread
        // count is a plain COUNT and a late-arriving message cannot be skipped.
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// Whether a renting in this state still accepts new messages. A finished
        /// or cancelled renting keeps its history readable but is closed to
        /// posting — the conversation exists to run an upcoming or ongoing
        /// rental. Both send paths (agency and customer) call this.
        /// </summary>
        /// <remarks>
        /// The thread-list queries cannot call it — EF has to translate the test
        /// into SQL — so they repeat the same states inline as <c>IsOpen</c>.
        /// Change this rule and those projections have to change with it.
        /// </remarks>
        public static bool CanPostTo(RentingState state)
            => state is RentingState.NotYet or RentingState.InProgress;
    }
}
