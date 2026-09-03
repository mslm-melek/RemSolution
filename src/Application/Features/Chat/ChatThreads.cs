using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Chat
{
    /// <summary>
    /// The single definition of "the messages of this thread". A thread hangs off
    /// a hire or off a confirmed hold, so which column to filter on is a branch —
    /// stated once here rather than in each of the six read and write paths, which
    /// is what keeps the agency's inbox and the customer's from disagreeing about
    /// what a thread is.
    /// </summary>
    internal static class ChatThreads
    {
        public static IQueryable<ChatMessage> InThread(
            this IQueryable<ChatMessage> messages, ChatSubjectKind subject, int id) =>
            subject == ChatSubjectKind.Reservation
                ? messages.Where(m => m.ReservationId == id)
                : messages.Where(m => m.RentingId == id);

        /// <summary>Points a new message at the right booking.</summary>
        public static void SetThread(this ChatMessage message, ChatSubjectKind subject, int id)
        {
            if (subject == ChatSubjectKind.Reservation)
            {
                message.ReservationId = id;
            }
            else
            {
                message.RentingId = id;
            }
        }
    }
}
