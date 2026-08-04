namespace RemSolution.Domain.Enums
{
    /// <summary>
    /// The kind of record a notification points at, paired with
    /// <c>Notification.SubjectId</c>. Deliberately not a set of nullable FKs: a
    /// notification is a message about something, not a relationship to it, and
    /// the subject may be soft-deleted or cancelled long before the notification
    /// is read. The SPA link is what actually navigates.
    /// </summary>
    public enum NotificationSubject
    {
        Car = 1,
        Renting = 2,
        Reservation = 3,
        Client = 4,
    }
}
