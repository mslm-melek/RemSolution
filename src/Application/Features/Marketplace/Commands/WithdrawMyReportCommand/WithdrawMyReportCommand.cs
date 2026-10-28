using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.AgencyReport;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Marketplace.Commands.WithdrawMyReportCommand
{
    /// <summary>
    /// The customer closes their own report before the platform has decided —
    /// it was sorted out with the agency, or it no longer matters to them. A
    /// reason is required: the platform and the agency both read it, and a
    /// complaint that vanishes without a word looks like one that was pressured
    /// away.
    /// <para>
    /// Costs the agency nothing, like a dismissal: nobody found it at fault. Not
    /// final — the customer can reopen it once (ReopenMyReportCommand), for when
    /// what got it dropped does not happen.
    /// </para>
    /// </summary>
    [Authorize(Policy = Policies.CustomerOnly)]
    // Audited like the verdict it pre-empts: if a withdrawal is later said to
    // have been pressured, who closed it and when is on record.
    [Auditable("WithdrawAgencyReport", "AgencyReport")]
    public record WithdrawMyReportCommand(int Id, string Reason) : IRequest;

    public class WithdrawMyReportCommandHandler : IRequestHandler<WithdrawMyReportCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public WithdrawMyReportCommandHandler(
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

        public async Task Handle(WithdrawMyReportCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var report = await _context.AgencyReports
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.ReporterUserId == userId, cancellationToken);

            Guard.Against.NotFound(request.Id, report);

            // Throws DomainRuleException once the platform has decided: a verdict
            // cannot be withdrawn from under the agency it cleared or condemned.
            report.Withdraw(request.Reason, _dateTime.GetUtcNow().UtcDateTime, userId);

            await _context.SaveChangesAsync(cancellationToken);

            // The platform is told before the agency's tenant is pushed below.
            await AgencyReportAlerts.NotifyPlatformAsync(
                _notifications, _context, report,
                NotificationMessages.PlatformReportWithdrawn, $"withdrawn:{report.ResolvedAt:O}", cancellationToken);

            // The agency was told when the report arrived, so it is told when it
            // goes away. After the commit, and under the agency's tenant pushed by
            // hand, exactly as CreateMyReportCommand does.
            using var _ = AmbientTenant.Push(report.AgencyId);

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.AgencyReport,
                    NotificationMessages.AgencyReportWithdrawn,
                    Permission: null,
                    NotificationSubject.AgencyReport,
                    report.Id,
                    "/my-agency/reports",
                    new NotificationArgs().Set("reference", $"#{report.Id}"),
                    // Timed: a reopened report can be withdrawn a second time,
                    // and the agency is owed that alert too.
                    DedupToken: $"withdrawn:{report.ResolvedAt:O}"),
                cancellationToken);
        }
    }

    public class WithdrawMyReportCommandValidator : AbstractValidator<WithdrawMyReportCommand>
    {
        public WithdrawMyReportCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            // The reason is both the outcome and a line on the thread, so it has
            // to fit the tighter of the two.
            RuleFor(v => v.Reason)
                .NotEmpty()
                .MaximumLength(Math.Min(
                    Domain.Entities.AgencyReport.MaxResolutionLength,
                    Domain.Entities.AgencyReport.MaxMessageLength));
        }
    }
}
