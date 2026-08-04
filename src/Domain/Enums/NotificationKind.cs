namespace RemSolution.Domain.Enums
{
    /// <summary>
    /// What a notification is about. The kind is the notification's identity for
    /// every purpose: it picks the message the SPA renders (one Transloco key per
    /// kind, interpolated with the row's arguments) and the mail the server
    /// composes, so adding a kind means adding those strings — never a new column
    /// here.
    /// <para>
    /// Kinds split into two audiences, and the split is deliberate: the first
    /// three are the agency's own work queue (in-app, optionally mailed to
    /// staff); the last three are messages to a client, which exist only as mail
    /// plus the row that records having sent it.
    /// </para>
    /// Values are persisted, so they are explicit and never reused.
    /// </summary>
    public enum NotificationKind
    {
        /// <summary>
        /// A recurring car expense — maintenance, insurance, technical
        /// inspection, road tax — is due or overdue for a car, by date or by
        /// odometer. Driven by the expense types the agency marked
        /// <c>WithNotif</c>; see the due calculator.
        /// </summary>
        CarExpenseDue = 1,

        /// <summary>A hire is past its end date and the car has not come back.</summary>
        RentingOverdue = 2,

        /// <summary>A confirmed hold starts within the agency's lead time.</summary>
        ReservationUpcoming = 3,

        /// <summary>
        /// Client reminder: their booking starts in a few days. Covers a
        /// confirmed reservation as well as a hire — the message key says which
        /// (see NotificationMessages), because to the customer it is the same
        /// heads-up about the same car on the same day.
        /// </summary>
        RentingStartingSoon = 4,

        /// <summary>Client reminder: their rental ends in a few days.</summary>
        RentingEndingSoon = 5,

        /// <summary>
        /// Client notice that they are late returning the car. Only ever sent by
        /// hand from the client list or detail screen — the agency decides when a
        /// late return is worth a letter, so nothing sends this on a schedule.
        /// </summary>
        RentingLateNotice = 6,
    }
}
