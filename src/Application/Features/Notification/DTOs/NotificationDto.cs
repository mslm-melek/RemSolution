using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Notification.DTOs
{
    /// <summary>
    /// One alert in the reader's inbox. Carries the ingredients of the message
    /// rather than the message: the SPA resolves
    /// <c>notifications.message.{messageKey}</c> and interpolates
    /// <see cref="Args"/> into it, so the same row reads in whichever language
    /// the user is in — including after they switch — and a reworded alert
    /// applies to the history too.
    /// </summary>
    public class NotificationDto
    {
        public int Id { get; init; }

        /// <summary>Drives the icon and the severity colour, and filters the list.</summary>
        public NotificationKind Kind { get; init; }

        /// <summary>Selects the wording; see <c>NotificationMessages</c>.</summary>
        public string MessageKey { get; init; } = string.Empty;

        /// <summary>
        /// Interpolation values, flattened out of the stored JSON by the query so
        /// the client never parses a string field. Values named with a
        /// <c>…Date</c> suffix are ISO dates for the client to format.
        /// </summary>
        public IDictionary<string, string> Args { get; init; } = new Dictionary<string, string>();

        public NotificationSubject SubjectType { get; init; }
        public int? SubjectId { get; init; }

        /// <summary>SPA route this opens; null when there is nothing to open.</summary>
        public string? Link { get; init; }

        public DateTime CreatedAt { get; init; }
        public DateTime? ReadAt { get; init; }

        public bool IsRead => ReadAt != null;
    }
}
