using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Chat.Commands.SendChatMessageCommand
{
    // The agency answering a client on one of its rentings. Whoever sends it,
    // the message is attributed to the agency side (AuthorKind.Agency) with the
    // staff member's name snapshotted — the client talks to "the agency", not to
    // a rotating cast of desk users.
    [Authorize(Policy = Permissions.ChatSend)]
    [RequiresFeature(FeatureFlags.Chat)]
    public record SendChatMessageCommand : IRequest<int>
    {
        public int RentingId { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public SendChatMessageCommandHandler(
            IApplicationDbContext context, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
        {
            // Tenant-filtered: another agency's renting reads as absent.
            var renting = await _context.Rentings
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.RentingId, cancellationToken);

            Guard.Against.NotFound(request.RentingId, renting);

            if (!ChatMessage.CanPostTo(renting.RentingState))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.RentingId),
                        "This renting is closed, so its conversation is read-only.")
                });
            }

            var entity = new ChatMessage
            {
                RentingId = request.RentingId,
                AuthorKind = ChatAuthorKind.Agency,
                SenderUserId = _user.Id,
                SenderName = _user.UserName,
                Body = request.Body.Trim(),
                SentAt = _dateTime.GetUtcNow().UtcDateTime,
            };

            _context.ChatMessages.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Chat.Commands.SendChatMessageCommand
{
    public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
    {
        public SendChatMessageCommandValidator()
        {
            RuleFor(v => v.RentingId).GreaterThan(0);
            RuleFor(v => v.Body).NotEmpty().MaximumLength(2000);
        }
    }
}
