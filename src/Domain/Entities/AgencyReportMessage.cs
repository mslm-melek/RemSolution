using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One line on a report after it was raised: the customer adding to their
    /// account, the platform asking them something, or the customer closing or
    /// reopening it (<see cref="Kind"/>). A conversation between the customer and
    /// the platform only — the agency reads it, but it is not a party to it.
    /// <para>
    /// Platform-level like the report it hangs off (see <see cref="AgencyReport"/>).
    /// </para>
    /// </summary>
    public class AgencyReportMessage : BaseEntity
    {
        public int AgencyReportId { get; private set; }
        public virtual AgencyReport? AgencyReport { get; private set; }

        public AgencyReportMessageKind Kind { get; private set; } = AgencyReportMessageKind.Message;

        /// <summary>Written by the platform administrator; otherwise by the customer.</summary>
        public bool FromPlatform { get; private set; }

        /// <summary>Identity user id of the writer. Cleared on the customer's erasure.</summary>
        public string? AuthorUserId { get; private set; }

        /// <summary>Empty once the customer's personal data is erased (see <see cref="Erase"/>).</summary>
        public string Body { get; private set; } = string.Empty;

        /// <summary>Instant (recorded from the clock), not a wall-clock date.</summary>
        public DateTime SentAt { get; private set; }

        // EF materialisation.
        private AgencyReportMessage() { }

        // Only the report builds these; see AgencyReport for the rules.
        internal AgencyReportMessage(
            AgencyReportMessageKind kind, bool fromPlatform, string body, DateTime sentAt, string? authorUserId)
        {
            Kind = kind;
            FromPlatform = fromPlatform;
            Body = body;
            SentAt = sentAt;
            AuthorUserId = authorUserId;
        }

        /// <summary>
        /// Forgets who wrote it and what they wrote. Free text is where a customer
        /// puts a phone number or a name, so unlike the report's opening account
        /// the follow-ups do not survive the erasure; the row stays, so the thread
        /// still shows that something was said and when.
        /// </summary>
        internal void Erase()
        {
            AuthorUserId = null;
            Body = string.Empty;
        }
    }
}
