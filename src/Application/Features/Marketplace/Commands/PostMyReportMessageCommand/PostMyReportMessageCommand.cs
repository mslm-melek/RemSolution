using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.AgencyReport;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Marketplace.Commands.PostMyReportMessageCommand
{
    /// <summary>
    /// The customer writes to the platform on a report of their own while it is
    /// still open — to add to their account, or to answer the platform. The
    /// entity refuses once the report is closed.
    /// </summary>
    [Authorize(Policy = Policies.CustomerOnly)]
    public record PostMyReportMessageCommand(int Id, string Body) : IRequest<int>;

    public class PostMyReportMessageCommandHandler : IRequestHandler<PostMyReportMessageCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public PostMyReportMessageCommandHandler(
            IApplicationDbContext context, INotificationService notifications, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _notifications = notifications;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(PostMyReportMessageCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            // Someone else's report is indistinguishable from a missing one.
            var report = await _context.AgencyReports
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.ReporterUserId == userId, cancellationToken);

            Guard.Against.NotFound(request.Id, report);

            var message = report.PostFromCustomer(
                request.Body, _dateTime.GetUtcNow().UtcDateTime, userId);

            await _context.SaveChangesAsync(cancellationToken);

            // Per message: each one is something the platform has to read.
            await AgencyReportAlerts.NotifyPlatformAsync(
                _notifications, _context, report,
                NotificationMessages.PlatformReportCustomerWrote, $"message:{message.Id}", cancellationToken);

            return message.Id;
        }
    }

    public class PostMyReportMessageCommandValidator : AbstractValidator<PostMyReportMessageCommand>
    {
        public PostMyReportMessageCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Body)
                .NotEmpty()
                .MaximumLength(Domain.Entities.AgencyReport.MaxMessageLength);
        }
    }
}
