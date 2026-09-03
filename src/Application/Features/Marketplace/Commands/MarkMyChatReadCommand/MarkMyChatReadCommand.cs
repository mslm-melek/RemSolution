using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Chat;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Marketplace.Commands.MarkMyChatReadCommand
{
    // Mirror of MarkChatReadCommand for the customer: stamps the AGENCY's unread
    // messages in the customer's own thread. Ownership is proven by the booking's
    // Client → MarketplaceUserId link, then the update acts as that agency so the
    // tenant filter matches the rows it is about to write.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record MarkMyChatReadCommand(ChatSubjectKind Subject, int Id) : IRequest;

    public class MarkMyChatReadCommandHandler : IRequestHandler<MarkMyChatReadCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public MarkMyChatReadCommandHandler(
            IApplicationDbContext context, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task Handle(MarkMyChatReadCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var agencyId = request.Subject == ChatSubjectKind.Reservation
                ? await _context.Reservations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.Id == request.Id
                                && r.Client != null
                                && r.Client.MarketplaceUserId == userId)
                    .Select(r => (int?)r.AgencyId)
                    .FirstOrDefaultAsync(cancellationToken)
                : await _context.Rentings
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.Id == request.Id
                                && r.Client != null
                                && r.Client.MarketplaceUserId == userId)
                    .Select(r => (int?)r.AgencyId)
                    .FirstOrDefaultAsync(cancellationToken);

            // Not the customer's booking (or no such booking): nothing to mark.
            if (agencyId is null)
            {
                return;
            }

            using var _ = AmbientTenant.Push(agencyId.Value);

            var unread = await _context.ChatMessages
                .InThread(request.Subject, request.Id)
                .Where(m => m.AuthorKind == ChatAuthorKind.Agency && m.ReadAt == null)
                .ToListAsync(cancellationToken);

            if (unread.Count == 0)
            {
                return;
            }

            var now = _dateTime.GetUtcNow().UtcDateTime;

            foreach (var message in unread)
            {
                message.ReadAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
