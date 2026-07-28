using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand
{
    // The customer side of a renting's conversation. A customer has no tenant, so
    // the renting is loaded cross-tenant and identity is proven by the link that
    // already exists: the renting's Client row must carry this user's
    // MarketplaceUserId. The write then acts as the renting's agency so the
    // AgencyId stamp and the tenant filter both target it.
    //
    // Deliberately NOT gated on the agency's Chat feature: the customer is
    // replying inside their own booking. An agency without the feature simply
    // never opens a thread, so there is nothing to reply to.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record SendCustomerChatMessageCommand : IRequest<int>
    {
        public int RentingId { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    public class SendCustomerChatMessageCommandHandler
        : IRequestHandler<SendCustomerChatMessageCommand, int>
    {
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

            var renting = await _context.Rentings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == request.RentingId
                            && r.Client != null
                            && r.Client.MarketplaceUserId == userId)
                .Select(r => new
                {
                    r.Id,
                    r.AgencyId,
                    r.RentingState,
                    ClientFirstName = r.Client!.FirstName,
                    ClientLastName = r.Client!.LastName,
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Someone else's renting is indistinguishable from a missing one.
            Guard.Against.NotFound(request.RentingId, renting);

            if (!ChatMessage.CanPostTo(renting.RentingState))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.RentingId),
                        "This renting is closed, so its conversation is read-only.")
                });
            }

            using var _ = AmbientTenant.Push(renting.AgencyId);

            var entity = new ChatMessage
            {
                RentingId = renting.Id,
                AuthorKind = ChatAuthorKind.Client,
                SenderUserId = userId,
                SenderName = ((renting.ClientFirstName ?? string.Empty)
                    + " " + (renting.ClientLastName ?? string.Empty)).Trim(),
                Body = request.Body.Trim(),
                SentAt = _dateTime.GetUtcNow().UtcDateTime,
            };

            _context.ChatMessages.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand
{
    public class SendCustomerChatMessageCommandValidator : AbstractValidator<SendCustomerChatMessageCommand>
    {
        public SendCustomerChatMessageCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.Body).NotEmpty().MaximumLength(2000);
        }
    }
}
