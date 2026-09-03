using System.Globalization;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Notifications;

/// <summary>
/// The "a request is waiting for you" alert, raised from the two paths that can
/// open one (the agency's own screen and the marketplace) and again by the sweep
/// as the hold runs out. Shared so both arrivals produce the same row: the
/// notification is about the request, not about who typed it.
/// </summary>
public static class ReservationAlerts
{
    /// <summary>
    /// Raises the alert for a request that has just arrived. Call it AFTER the
    /// booking is committed — the service writes its own rows, and a mail server
    /// being down must not undo a reservation.
    /// </summary>
    public static Task RaiseRequestedAsync(
        INotificationService notifications,
        int reservationId,
        int? clientId,
        string? clientFirstName,
        string? clientLastName,
        string? carMatricule,
        string? carModelName,
        DateTime? startDate,
        CancellationToken cancellationToken)
    {
        var args = new NotificationArgs()
            .Set("car", CarLabel(carModelName, carMatricule))
            .Set("client", PersonLabel(clientFirstName, clientLastName))
            .SetDate("startDate", startDate);

        return notifications.NotifyStaffAsync(
            new StaffNotification(
                NotificationKind.ReservationPending,
                NotificationMessages.ReservationPending,
                // The people who can answer it, not merely read it: an alert that
                // asks for a decision goes to whoever can take it.
                Permissions.ReservationUpdate,
                NotificationSubject.Reservation,
                reservationId,
                $"/reservation/{reservationId}",
                args,
                // Once per request, ever: the subject already makes it unique, and
                // a request only arrives once.
                DedupToken: "requested",
                ClientId: clientId),
            cancellationToken);
    }

    public static string CarLabel(string? modelName, string? matricule)
    {
        var label = string.Join(" — ", new[] { modelName, matricule }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrWhiteSpace(label) ? "—" : label;
    }

    public static string PersonLabel(string? firstName, string? lastName)
    {
        var label = $"{firstName} {lastName}".Trim();

        return string.IsNullOrWhiteSpace(label) ? "—" : label;
    }

    public static string IsoDate(DateTime? value) =>
        value?.ToString(NotificationArgs.IsoDateFormat, CultureInfo.InvariantCulture) ?? "-";
}
