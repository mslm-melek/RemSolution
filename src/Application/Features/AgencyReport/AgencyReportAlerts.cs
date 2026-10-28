using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencyReport
{
    /// <summary>
    /// Who hears about a report besides the agency: the platform administrators
    /// (in the app) when the customer acts, and the customer (by mail, at the
    /// address they gave) when the platform does. One place, so every command
    /// words the same event the same way.
    /// <para>
    /// Callers run these after their commit and before pushing the agency's
    /// tenant for its own alert — neither of these belongs to an agency.
    /// </para>
    /// </summary>
    public static class AgencyReportAlerts
    {
        public const string PlatformLink = "/agency-reports";
        public const string CustomerLink = "/my-bookings?tab=reports";

        public static async Task NotifyPlatformAsync(
            INotificationService notifications,
            IApplicationDbContext context,
            Domain.Entities.AgencyReport report,
            string messageKey,
            string dedupToken,
            CancellationToken cancellationToken)
        {
            await notifications.NotifyPlatformAsync(
                new PlatformAlert(
                    NotificationKind.AgencyReport,
                    messageKey,
                    NotificationSubject.AgencyReport,
                    report.Id,
                    PlatformLink,
                    new NotificationArgs()
                        .Set("reference", $"#{report.Id}")
                        .Set("agency", await AgencyNameAsync(context, report.AgencyId, cancellationToken)),
                    dedupToken),
                cancellationToken);
        }

        /// <summary>
        /// Writes to the customer when they left an address on the report; does
        /// nothing otherwise. <paramref name="text"/> is what the platform said —
        /// the reply or the verdict's note — so the mail can be read on its own.
        /// </summary>
        public static async Task MailCustomerAsync(
            INotificationService notifications,
            IApplicationDbContext context,
            Domain.Entities.AgencyReport report,
            string messageKey,
            string text,
            CancellationToken cancellationToken)
        {
            if (report.NotifyEmail is not string email)
            {
                return;
            }

            await notifications.MailCustomerAsync(
                new CustomerMail(
                    email,
                    report.ReporterUserId,
                    messageKey,
                    new NotificationArgs()
                        .Set("reference", $"#{report.Id}")
                        .Set("agency", await AgencyNameAsync(context, report.AgencyId, cancellationToken))
                        .Set("text", text),
                    CustomerLink),
                cancellationToken);
        }

        // Agencies are platform-level rows, readable without a tenant.
        private static async Task<string> AgencyNameAsync(
            IApplicationDbContext context, int agencyId, CancellationToken cancellationToken) =>
            await context.Agencies
                .AsNoTracking()
                .Where(a => a.Id == agencyId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "—";
    }
}
