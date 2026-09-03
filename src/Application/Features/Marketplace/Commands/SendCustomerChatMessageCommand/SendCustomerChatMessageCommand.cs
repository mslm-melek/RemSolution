using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Chat;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand
{
    // The customer side of a booking's conversation. A customer has no tenant, so
    // the booking is loaded cross-tenant and identity is proven by the link that
    // already exists: its Client row must carry this user's MarketplaceUserId.
    // The write then acts as the booking's agency so the AgencyId stamp and the
    // tenant filter both target it.
    //
    // Deliberately NOT gated on the agency's Chat feature: the customer is
    // replying inside their own booking. An agency without the feature simply
    // never opens a thread, so there is nothing to reply to.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record SendCustomerChatMessageCommand : IRequest<int>
    {
        public ChatSubjectKind Subject { get; init; }
        public int Id { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    public class SendCustomerChatMessageCommandHandler
        : IRequestHandler<SendCustomerChatMessageCommand, int>
    {
        /// <summary>Whose booking it is, whether it is open, and who they are.</summary>
        private sealed record Owner(int AgencyId, bool IsOpen, string? FirstName, string? LastName);

        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public SendCustomerChatMessageCommandHandler(
            IApplicationDbContext context, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(
            SendCustomerChatMessageCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var owner = await LoadOwnerAsync(request, userId, cancellationToken);

            // Someone else's booking is indistinguishable from a missing one.
            Guard.Against.NotFound(request.Id, owner);

            if (!owner.IsOpen)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This booking is closed, so its conversation is read-only.")
                });
            }

            using var _ = AmbientTenant.Push(owner.AgencyId);

            var entity = new ChatMessage
            {
                AuthorKind = ChatAuthorKind.Client,
                SenderUserId = userId,
                SenderName = ((owner.FirstName ?? string.Empty)
                    + " " + (owner.LastName ?? string.Empty)).Trim(),
                Body = request.Body.Trim(),
                SentAt = _dateTime.GetUtcNow().UtcDateTime,
            };

            entity.SetThread(request.Subject, request.Id);

            _context.ChatMessages.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }

        // The open/closed rule is the aggregate's (ChatMessage.CanPostTo), but it
        // cannot be called in SQL, so it is applied to the projected state here.
        private async Task<Owner?> LoadOwnerAsync(
            SendCustomerChatMessageCommand request, string userId, CancellationToken cancellationToken)
        {
            if (request.Subject == ChatSubjectKind.Reservation)
            {
                var hold = await _context.Reservations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => r.Id == request.Id
                                && r.Client != null
                                && r.Client.MarketplaceUserId == userId)
                    .Select(r => new { r.AgencyId, r.Status, r.Client!.FirstName, r.Client!.LastName })
                    .FirstOrDefaultAsync(cancellationToken);

                return hold is null
                    ? null
                    : new Owner(hold.AgencyId, ChatMessage.CanPostTo(hold.Status), hold.FirstName, hold.LastName);
            }

            var hire = await _context.Rentings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == request.Id
                            && r.Client != null
                            && r.Client.MarketplaceUserId == userId)
                .Select(r => new { r.AgencyId, r.RentingState, r.Client!.FirstName, r.Client!.LastName })
                .FirstOrDefaultAsync(cancellationToken);

            return hire is null
                ? null
                : new Owner(hire.AgencyId, ChatMessage.CanPostTo(hire.RentingState), hire.FirstName, hire.LastName);
        }
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand
{
    public class SendCustomerChatMessageCommandValidator : AbstractValidator<SendCustomerChatMessageCommand>
    {
        public SendCustomerChatMessageCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Subject).IsInEnum();
            RuleFor(v => v.Body).NotEmpty().MaximumLength(2000);
        }
    }
}
