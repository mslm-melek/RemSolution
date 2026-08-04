using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One thing the agency (or one of its clients) was told. Every notification
    /// the system produces lands here, whichever way it was delivered, which is
    /// what makes this table both the staff inbox and the record of what was
    /// mailed to whom.
    /// <para>
    /// Two shapes share the row. A staff notification has a
    /// <see cref="RecipientUserId"/> and is read in the app; a client message has
    /// none and only a <see cref="RecipientEmail"/> — nothing renders it in the
    /// SPA, it exists so the agency can see the reminder went out and so the
    /// sweep does not send it twice.
    /// </para>
    /// <para>
    /// The text is NOT stored. <see cref="Kind"/> picks the message and
    /// <see cref="ArgsJson"/> carries the values interpolated into it, so a
    /// notification reads in whatever language its reader has chosen — including
    /// after they switch — and an improved wording applies to the whole history.
    /// </para>
    /// </summary>
    public class Notification : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public NotificationKind Kind { get; set; }

        /// <summary>
        /// Which wording this row renders, as a logical key both renderers
        /// resolve (<c>notifications.message.{key}</c> in the SPA,
        /// <c>Notification.{key}.Subject/.Body</c> in the resx). Separate from
        /// <see cref="Kind"/> because one kind can need several wordings — a
        /// service due by date and the same service due by odometer are the same
        /// alert with the same icon and a different sentence — and splitting the
        /// enum for that would make presentation follow grammar.
        /// <para>
        /// Stored, so these keys are a contract: rename one and the history stops
        /// rendering. See <c>NotificationMessages</c> for the list.
        /// </para>
        /// </summary>
        public string MessageKey { get; set; } = string.Empty;

        /// <summary>
        /// Identity user id of the staff member this is addressed to. Null on a
        /// row that exists only to record a message sent to a client (see the
        /// class remarks) — the notification centre reads by this column, so
        /// those rows never appear in anybody's inbox.
        /// </summary>
        public string? RecipientUserId { get; set; }

        /// <summary>
        /// Where the mail was addressed, snapshotted at send time. Set on both
        /// shapes whenever mail was actually attempted; null means in-app only.
        /// Kept even if the client later changes address, because the question it
        /// answers is "where did we write to", not "where would we write now".
        /// </summary>
        public string? RecipientEmail { get; set; }

        public NotificationSubject SubjectType { get; set; }

        /// <summary>
        /// Id of the subject record. Nullable so a subject that has gone away
        /// does not have to invent one; the link below is what navigates.
        /// </summary>
        public int? SubjectId { get; set; }

        /// <summary>
        /// The client this concerns, when there is one — the recipient of a client
        /// message, or the renter behind a staff alert. Restrict on delete, like
        /// every other client reference: a client is archived, never removed.
        /// </summary>
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }

        /// <summary>
        /// Values interpolated into the message, as a flat JSON object of string
        /// properties (car matricule, client name, a formatted date, a number of
        /// days…). Flat and stringly-typed on purpose: both renderers — Transloco
        /// in the SPA and the resx localizer on the server — take named string
        /// arguments, so anything richer would only have to be flattened again.
        /// </summary>
        public string? ArgsJson { get; set; }

        /// <summary>SPA route this notification opens (e.g. <c>/renting/42</c>).</summary>
        public string? Link { get; set; }

        /// <summary>
        /// Idempotency key for the sweep, unique within the agency. It encodes the
        /// recipient, the kind, the subject and a time bucket, so re-running the
        /// job — hourly, after a retry, after a crash — cannot tell anybody the
        /// same thing twice, while a warning that is still true next week does get
        /// repeated when its bucket rolls over. Built by the sweep, never parsed.
        /// </summary>
        public string DedupKey { get; set; } = string.Empty;

        /// <summary>
        /// UTC, like every domain DateTime. Set explicitly rather than read from
        /// the audit stamp: the sweep needs it to order and bucket rows, and the
        /// audit columns are infrastructure's to write.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the recipient read it in the app; null while unread.</summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// When the mail left. Null with a <see cref="RecipientEmail"/> set means
        /// the send was attempted and failed — the notification still stands, and
        /// the agency can see it never went out.
        /// </summary>
        public DateTime? EmailSentAt { get; set; }

        /// <summary>
        /// Who pressed send, for the notices a person triggers by hand. Null on
        /// everything the sweep produced, which is how the two are told apart.
        /// </summary>
        public string? SentByUserId { get; set; }

        /// <summary>
        /// Marks the row read. Idempotent: a second call keeps the first instant,
        /// because "when did you see this" has one answer and the SPA marks a
        /// visible list read on every poll tick.
        /// </summary>
        public void MarkRead(DateTime readAt)
        {
            ReadAt ??= readAt;
        }
    }
}
