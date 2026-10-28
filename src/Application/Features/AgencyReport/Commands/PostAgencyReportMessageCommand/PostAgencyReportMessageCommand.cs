using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.AgencyReport.Commands.PostAgencyReportMessageCommand
{
    /// <summary>
    /// The platform administrator writes to the customer on an open report —
    /// usually to ask for what the decision still needs. The agency reads the
    /// thread but is not a party to it. The customer is mailed the reply when
    /// they left an address on the report.
    /// </summary>
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record PostAgencyReportMessageCommand(int Id, string Body) : IRequest<int>;

    public class PostAgencyReportMessageCommandHandler : IRequestHandler<PostAgencyReportMessageCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public PostAgencyReportMessageCommandHandler(
            IApplicationDbContext context, INotificationService notifications, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _notifications = notifications;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(PostAgencyReportMessageCommand request, CancellationToken cancellationToken)
        {
            var report = await _context.AgencyReports
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, report);

            var message = report.PostFromPlatform(
                request.Body, _dateTime.GetUtcNow().UtcDateTime, _user.Id);

            await _context.SaveChangesAsync(cancellationToken);

            await AgencyReportAlerts.MailCustomerAsync(
                _notifications, _context, report,
                NotificationMessages.CustomerReportAnswered, message.Body, cancellationToken);

            return message.Id;
        }
    }

    public class PostAgencyReportMessageCommandValidator : AbstractValidator<PostAgencyReportMessageCommand>
    {
        public PostAgencyReportMessageCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Body)
                .NotEmpty()
                .MaximumLength(Domain.Entities.AgencyReport.MaxMessageLength);
        }
    }
}
