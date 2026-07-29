using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Client.DTOs
{
    /// <summary>
    /// What an invite attempt did. The agency needs all three parts: whether an
    /// account now exists, whether it was <em>this</em> click that created or
    /// reset it, and whether the mail actually went out — a temporary password
    /// issued into a dead mail server is a client who cannot sign in and an
    /// agency who thinks they can.
    /// </summary>
    public class ClientInvitationDto
    {
        public ClientAccountOutcome Outcome { get; init; }

        /// <summary>
        /// True when a credentials email left the building. False both when
        /// there was nothing to send (the account is already the customer's
        /// own) and when sending failed — <see cref="Outcome"/> tells them
        /// apart.
        /// </summary>
        public bool EmailSent { get; init; }

        /// <summary>The address the invitation was addressed to, if any.</summary>
        public string? Email { get; init; }
    }
}
