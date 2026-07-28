using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Chat.DTOs
{
    public class ChatMessageDto
    {
        public int Id { get; init; }
        public int RentingId { get; init; }
        public ChatAuthorKind AuthorKind { get; init; }
        public string? SenderName { get; init; }
        public string? Body { get; init; }
        public DateTime SentAt { get; init; }
        // Null while the other side has not opened the thread.
        public DateTime? ReadAt { get; init; }
    }

    // One renting's conversation as it appears in the agency's inbox list.
    public class ChatThreadDto
    {
        public int RentingId { get; init; }
        public int? CarId { get; init; }
        public string? CarMatricule { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public RentingState RentingState { get; init; }
        // Null when the renting has no message yet (threads are listed for every
        // ongoing/upcoming renting so the desk can start the conversation).
        public string? LastMessagePreview { get; init; }
        public DateTime? LastMessageAt { get; init; }
        public ChatAuthorKind? LastMessageAuthorKind { get; init; }
        // Messages from the other side this reader has not marked read yet.
        public int UnreadCount { get; init; }
        // Whether new messages may still be posted — a finished or cancelled
        // renting keeps its history but is read-only.
        public bool IsOpen { get; init; }
    }
}
