namespace RemSolution.Application.Common.Notifications;

/// <summary>
/// The wordings a notification can render, as logical keys stored on the row
/// (see <c>Notification.MessageKey</c>). Each one must exist in three places or
/// it renders as its own key:
/// <list type="bullet">
/// <item><c>notifications.message.{key}</c> in the SPA translation files;</item>
/// <item><c>Notification.{key}.Subject</c> in the shared resx (mail subject);</item>
/// <item><c>Notification.{key}.Body</c> in the shared resx (mail body).</item>
/// </list>
/// These strings are persisted, so they are a contract: renaming one silently
/// un-renders every notification already in the database.
/// </summary>
public static class NotificationMessages
{
    // ---- Fleet: a recurring expense type is coming due for a car -------------
    // Four wordings for one alert, because "in 9 days" and "800 km ago" are not
    // the same sentence and neither is the tense.
    public const string CarExpenseDueByDate = "carExpenseDueByDate";
    public const string CarExpenseOverdueByDate = "carExpenseOverdueByDate";
    public const string CarExpenseDueByDistance = "carExpenseDueByDistance";
    public const string CarExpenseOverdueByDistance = "carExpenseOverdueByDistance";

    // ---- Bookings the agency needs to act on --------------------------------
    public const string RentingOverdue = "rentingOverdue";
    public const string ReservationUpcoming = "reservationUpcoming";

    // ---- Written to the client ----------------------------------------------
    public const string ClientRentingStartingSoon = "clientRentingStartingSoon";
    public const string ClientRentingEndingSoon = "clientRentingEndingSoon";
    public const string ClientReservationStartingSoon = "clientReservationStartingSoon";
    public const string ClientRentingLateNotice = "clientRentingLateNotice";
}
