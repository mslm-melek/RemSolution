using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Chat.DTOs
{
    public class ChatMessageDto
    {
        public int Id { get; init; }
        // Exactly one of the two is set; Subject says which (see ChatMessage).
        public int? RentingId { get; init; }
        public int? ReservationId { get; init; }
        public ChatSubjectKind Subject { get; init; }
        public ChatAuthorKind AuthorKind { get; init; }
        public string? SenderName { get; init; }
        public string? Body { get; init; }
        public DateTime SentAt { get; init; }
        // Null while the other side has not opened the thread.
        public DateTime? ReadAt { get; init; }
    }

    // One booking's conversation as it appears in the agency's inbox list. The
    // booking is a hire or a confirmed hold: Subject says which, and the matching
    // id is the one that is set.
    public class ChatThreadDto
    {
        public ChatSubjectKind Subject { get; init; }
        public int? RentingId { get; init; }
        public int? ReservationId { get; init; }
        public int? CarId { get; init; }
        public string? CarMatricule { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        // Set on a hire's thread; a hold carries ReservationStatus instead.
        public RentingState? RentingState { get; init; }
        public ReservationStatus? ReservationStatus { get; init; }
        // Null when the booking has no message yet (threads are listed for every
        // live booking so the desk can start the conversation).
        public string? LastMessagePreview { get; init; }
        public DateTime? LastMessageAt { get; init; }
        public ChatAuthorKind? LastMessageAuthorKind { get; init; }
        // Messages from the other side this reader has not marked read yet.
        public int UnreadCount { get; init; }
        // Whether new messages may still be posted — a finished, cancelled or
        // lapsed booking keeps its history but is read-only.
        public bool IsOpen { get; init; }

        /// <summary>The id of whichever booking this thread hangs off.</summary>
        public int SubjectId => (Subject == ChatSubjectKind.Reservation ? ReservationId : RentingId) ?? 0;
    }
}
