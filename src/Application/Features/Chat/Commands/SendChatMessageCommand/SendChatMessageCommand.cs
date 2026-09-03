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
        public ChatSubjectKind Subject { get; init; }
        public int Id { get; init; }
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
            await EnsureOpenAsync(request, cancellationToken);

            var entity = new ChatMessage
            {
                AuthorKind = ChatAuthorKind.Agency,
                SenderUserId = _user.Id,
                SenderName = _user.UserName,
                Body = request.Body.Trim(),
                SentAt = _dateTime.GetUtcNow().UtcDateTime,
            };

            entity.SetThread(request.Subject, request.Id);

            _context.ChatMessages.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }

        // Tenant-filtered on both branches: another agency's booking reads as
        // absent rather than as a refusal.
        private async Task EnsureOpenAsync(
            SendChatMessageCommand request, CancellationToken cancellationToken)
        {
            if (request.Subject == ChatSubjectKind.Reservation)
            {
                var status = await _context.Reservations
                    .AsNoTracking()
                    .Where(r => r.Id == request.Id)
                    .Select(r => (ReservationStatus?)r.Status)
                    .FirstOrDefaultAsync(cancellationToken);

                Guard.Against.NotFound(request.Id, status);

                if (!ChatMessage.CanPostTo(status.Value))
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.Id),
                            "This reservation is not open to messages.")
                    });
                }

                return;
            }

            var state = await _context.Rentings
                .AsNoTracking()
                .Where(r => r.Id == request.Id)
                .Select(r => (RentingState?)r.RentingState)
                .FirstOrDefaultAsync(cancellationToken);

            Guard.Against.NotFound(request.Id, state);

            if (!ChatMessage.CanPostTo(state.Value))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This renting is closed, so its conversation is read-only.")
                });
            }
        }
    }
}

namespace RemSolution.Application.Features.Chat.Commands.SendChatMessageCommand
{
    public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
    {
        public SendChatMessageCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Subject).IsInEnum();
            RuleFor(v => v.Body).NotEmpty().MaximumLength(2000);
        }
    }
}
