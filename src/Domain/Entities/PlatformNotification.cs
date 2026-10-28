using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One thing a platform administrator was told, in the app only. The
    /// platform-level twin of <see cref="Notification"/>: that table is an
    /// <c>ITenantEntity</c>, and a platform administrator carries no agency claim,
    /// so its filter would hide every row from the person it was addressed to.
    /// <para>
    /// Same contract otherwise: the text is not stored, <see cref="MessageKey"/>
    /// and <see cref="ArgsJson"/> are rendered by the SPA, and
    /// <see cref="DedupKey"/> stops one event being announced twice.
    /// </para>
    /// </summary>
    public class PlatformNotification : BaseEntity
    {
        public string RecipientUserId { get; set; } = string.Empty;

        public NotificationKind Kind { get; set; }

        /// <summary>See <c>Notification.MessageKey</c> — stored, so a contract.</summary>
        public string MessageKey { get; set; } = string.Empty;

        public NotificationSubject SubjectType { get; set; }

        public int? SubjectId { get; set; }

        public string? ArgsJson { get; set; }

        /// <summary>SPA route this notification opens.</summary>
        public string? Link { get; set; }

        public string DedupKey { get; set; } = string.Empty;

        /// <summary>Instant (recorded from the clock).</summary>
        public DateTime CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }

        /// <summary>Idempotent, like <see cref="Notification.MarkRead"/>.</summary>
        public void MarkRead(DateTime readAt)
        {
            ReadAt ??= readAt;
        }
    }
}
