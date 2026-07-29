using RemSolution.Application.Common.Models;
using ClientEntity = RemSolution.Domain.Entities.Client;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Turns a client's email address into a customer-portal account, so the people
/// an agency rents to can sign in and see their own bookings, documents and
/// chat threads — the same surface a self-registered marketplace customer gets.
///
/// <para>
/// Two calls, deliberately separate, because they belong on opposite sides of a
/// commit: <see cref="LinkOrCreateAsync"/> writes (it creates the Identity user
/// and sets <c>Client.MarketplaceUserId</c>) and therefore has to run inside
/// the caller's transaction, while <see cref="SendCredentialsAsync"/> talks to
/// an SMTP server and must not — a mail sent for a renting that then rolls back
/// cannot be unsent, and a mail server that is slow or down must not fail the
/// booking. Call the first before <c>SaveChanges</c>, the second after
/// <c>Commit</c>.
/// </para>
/// </summary>
public interface IClientAccountService
{
    /// <summary>
    /// Links <paramref name="client"/> to the account for its email address,
    /// creating one when the address is new. Sets
    /// <c>client.MarketplaceUserId</c> on success — the caller still has to
    /// save. A client with no email, or one already linked, is a no-op.
    /// </summary>
    Task<ClientAccountResult> LinkOrCreateAsync(ClientEntity client, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="LinkOrCreateAsync"/>, but also re-issues a temporary
    /// password when the client is already linked to an account that has never
    /// been used — the "re-send the invitation" path, for a mail that was lost.
    /// An account whose owner has chosen their own password is never reset
    /// (<see cref="ClientAccountOutcome.AlreadyActive"/>): an agency must not be
    /// able to lock a customer out of an identity they also use elsewhere.
    /// </summary>
    Task<ClientAccountResult> ReinviteAsync(ClientEntity client, CancellationToken cancellationToken);

    /// <summary>
    /// Emails the credentials in <paramref name="result"/>, if it carries any.
    /// Returns whether a message was actually sent.
    /// <para>
    /// Never throws. Everything that reaches this method has already been
    /// committed, so an SMTP failure is a delivery problem, not a reason to
    /// undo a renting — it is logged, and the agency can re-send from the
    /// client screen.
    /// </para>
    /// </summary>
    Task<bool> SendCredentialsAsync(ClientAccountResult result, CancellationToken cancellationToken);
}
