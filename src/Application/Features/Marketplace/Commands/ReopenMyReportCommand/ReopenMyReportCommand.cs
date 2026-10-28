using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.AgencyReport;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Marketplace.Commands.ReopenMyReportCommand
{
    /// <summary>
    /// The customer takes back a withdrawal — typically because the refund or
    /// apology that got the report dropped never came. Without this, persuading a
    /// customer to withdraw would be a way out of arbitration.
    /// <para>
    /// Once per report, and within the reporting window of the withdrawal (see
    /// <c>AgencyReport.CanReopen</c>); a reason is required, and the agency is
    /// told, as it was told of the withdrawal.
    /// </para>
    /// </summary>
    [Authorize(Policy = Policies.CustomerOnly)]
    [Auditable("ReopenAgencyReport", "AgencyReport")]
    public record ReopenMyReportCommand(int Id, string Reason) : IRequest;

    public class ReopenMyReportCommandHandler : IRequestHandler<ReopenMyReportCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public ReopenMyReportCommandHandler(
            IApplicationDbContext context,
            INotificationService notifications,
            IUser user,
            TimeProvider dateTime)
        {
            _context = context;
            _notifications = notifications;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task Handle(ReopenMyReportCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var report = await _context.AgencyReports
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.ReporterUserId == userId, cancellationToken);

            Guard.Against.NotFound(request.Id, report);

            // Throws DomainRuleException unless it is a withdrawal not yet
            // reopened and still inside the window.
            report.Reopen(request.Reason, _dateTime.GetUtcNow().UtcDateTime, userId);

            await _context.SaveChangesAsync(cancellationToken);

            await AgencyReportAlerts.NotifyPlatformAsync(
                _notifications, _context, report,
                NotificationMessages.PlatformReportReopened, "reopened", cancellationToken);

            using var _ = AmbientTenant.Push(report.AgencyId);

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.AgencyReport,
                    NotificationMessages.AgencyReportReopened,
                    Permission: null,
                    NotificationSubject.AgencyReport,
                    report.Id,
                    "/my-agency/reports",
                    new NotificationArgs().Set("reference", $"#{report.Id}"),
                    DedupToken: "reopened"),
                cancellationToken);
        }
    }

    public class ReopenMyReportCommandValidator : AbstractValidator<ReopenMyReportCommand>
    {
        public ReopenMyReportCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Reason)
                .NotEmpty()
                .MaximumLength(Domain.Entities.AgencyReport.MaxMessageLength);
        }
    }
}
