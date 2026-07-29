using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Client.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.InviteClientCommand
{
    // Sends (or re-sends) the customer-portal invitation for a client the agency
    // already holds an email for. The create/update/renting paths do this
    // automatically; this is the button for when the first mail was lost, or
    // when a client recorded before the portal existed needs bringing online.
    //
    // Client.Update rather than a permission of its own: an invitation writes
    // the account link onto the client record, and whoever may edit that record
    // may decide it should have a login — the same reasoning as FlagClient.
    [Authorize(Policy = Permissions.ClientUpdate)]
    [RequiresFeature(FeatureFlags.Clients)]
    public record InviteClientCommand(int Id) : IRequest<ClientInvitationDto>;

    public class InviteClientCommandHandler : IRequestHandler<InviteClientCommand, ClientInvitationDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IClientAccountService _accounts;

        public InviteClientCommandHandler(IApplicationDbContext context, IClientAccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        public async Task<ClientInvitationDto> Handle(
            InviteClientCommand request, CancellationToken cancellationToken)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, client);

            if (string.IsNullOrWhiteSpace(client.Email))
            {
                // Nothing to invite: the UI only offers the action when an
                // address is on file, so this is a stale screen rather than a
                // failure worth a 400.
                return new ClientInvitationDto { Outcome = ClientAccountOutcome.None };
            }

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            // Reinvite, not LinkOrCreate: this is the one entry point allowed to
            // re-issue a temporary password, and only for an account that has
            // never been used (see IClientAccountService.ReinviteAsync).
            var account = await _accounts.ReinviteAsync(client, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var sent = await _accounts.SendCredentialsAsync(account, cancellationToken);

            return new ClientInvitationDto
            {
                Outcome = account.Outcome,
                EmailSent = sent,
                Email = account.Email,
            };
        }
    }
}
