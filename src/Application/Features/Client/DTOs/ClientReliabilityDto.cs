namespace RemSolution.Application.Features.Client.DTOs
{
    /// <summary>
    /// How dependable a client has been WITH THIS AGENCY, and the facts that say
    /// so. The counts travel with the score on purpose: a number nobody can
    /// account for is not something an agent can decide on, and "82" means
    /// nothing next to "one cancellation out of six bookings".
    /// <para>
    /// Deliberately per-agency. The cancellations of a customer at OTHER agencies
    /// are not this agency's business — the same reasoning that keeps
    /// <c>Client.IsFlagged</c> off the marketplace (see the Client entity).
    /// </para>
    /// </summary>
    public class ClientReliabilityDto
    {
        public int ClientId { get; init; }

        /// <summary>Everything they have ever booked here: holds and hires.</summary>
        public int Bookings { get; init; }

        /// <summary>Holds THEY called off. An agency's own cancellations are not counted.</summary>
        public int Cancellations { get; init; }

        /// <summary>Of those, the ones late enough to carry a fee.</summary>
        public int LateCancellations { get; init; }

        /// <summary>Bookings that ran: a hire that happened, or a hold that became one.</summary>
        public int Completed { get; init; }

        /// <summary>
        /// 100 for a clean record, less 15 per cancellation and another 15 when it
        /// was late, floored at 0. A points scale rather than a rate, because a
        /// rate reads as 0% for a first-time customer who cancels once — which
        /// says far more than the record supports.
        /// </summary>
        public int Score { get; init; }
    }
}
