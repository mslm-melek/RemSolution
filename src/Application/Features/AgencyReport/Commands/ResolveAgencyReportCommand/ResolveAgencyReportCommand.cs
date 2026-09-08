using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencyReport.Commands.ResolveAgencyReportCommand
{
    /// <summary>
    /// The platform administrator arbitrates a complaint. Upholding it costs the
    /// agency reliability points; dismissing it costs nothing. Either way a note
    /// is required — both sides are shown the outcome, and an outcome nobody
    /// explains is not arbitration.
    /// <para>
    /// No money moves in either direction (see <c>AgencyReport</c>).
    /// </para>
    /// </summary>
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    [Auditable("ResolveAgencyReport", "AgencyReport")]
    public record ResolveAgencyReportCommand(int Id, bool Upheld, string Note) : IRequest;

    public class ResolveAgencyReportCommandHandler : IRequestHandler<ResolveAgencyReportCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public ResolveAgencyReportCommandHandler(
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

        public async Task Handle(ResolveAgencyReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _context.AgencyReports
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, report);

            var now = _dateTime.GetUtcNow().UtcDateTime;

            // Throws DomainRuleException if it was already settled — an
            // arbitration that could be re-run would move the score twice.
            if (request.Upheld)
            {
                report.Uphold(request.Note, now, _user.Id);
            }
            else
            {
                report.Dismiss(request.Note, now, _user.Id);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // After the verdict is committed: the agency being told is not worth
            // undoing the decision over, and the notification service writes its
            // own rows. A platform administrator carries no agency claim, so the
            // tenant the alert belongs to has to be pushed explicitly.
            using var _ = AmbientTenant.Push(report.AgencyId);

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.AgencyReport,
                    report.Status == AgencyReportStatus.Upheld
                        ? NotificationMessages.AgencyReportUpheld
                        : NotificationMessages.AgencyReportDismissed,
                    // No module owns a complaint about the agency itself; it goes
                    // to the people who answer for it.
                    Permission: null,
                    NotificationSubject.AgencyReport,
                    report.Id,
                    "/my-agency/reports",
                    new NotificationArgs().Set("reference", $"#{report.Id}"),
                    DedupToken: "resolved"),
                cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.AgencyReport.Commands.ResolveAgencyReportCommand
{
    public class ResolveAgencyReportCommandValidator : AbstractValidator<ResolveAgencyReportCommand>
    {
        public ResolveAgencyReportCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Note)
                .NotEmpty()
                .MaximumLength(Domain.Entities.AgencyReport.MaxResolutionLength);
        }
    }
}
