using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Application.Features.Client.Validation;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.CreateClientCommand
{
    // ISensitiveRequest: carries identity-document numbers — never
    // destructured into logs by the pipeline behaviours.
    [Authorize(Policy = Permissions.ClientCreate)]
    [RequiresFeature(FeatureFlags.Clients)]
    public record CreateClientCommand : IRequest<int>, IClientPayload, ISensitiveRequest
    {
        // AgencyId is not accepted from the client: TenantEntityInterceptor
        // stamps it from the current tenant on insert.
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        // Setting this provisions the client's customer-portal account (see the
        // handler); leaving it blank keeps the client offline-only.
        public string? Email { get; init; }
        public DateTime? BirthDate { get; init; }
        public string? BirthPlace { get; init; }
        public int? BirthCountryId { get; init; }
        public string? CIN { get; init; }
        public DateTime? CINDeliveranceDate { get; init; }
        public string? CINDeliverancePlace { get; init; }
        public int? CINDeliveranceCountryId { get; init; }
        public string? PasseportNumber { get; init; }
        public DateTime? PasseportDeliveranceDate { get; init; }
        public string? PasseportDeliverancePlace { get; init; }
        public int? PasseportDeliveranceCountryId { get; init; }
        public string? DrivingLicenceNumber { get; init; }
        public DateTime? DrivingLicenceDeliveranceDate { get; init; }
        public string? DrivingLicenceDeliverancePlace { get; init; }
        public int? DrivingLicenceDeliveranceCountryId { get; init; }
        // The document image URLs are deliberately absent: they are owned by
        // UploadClientDocumentCommand, which manages the stored files'
        // lifecycle. Accepting them here would let callers plant arbitrary
        // URLs that a later upload would delete.
        public string? Description { get; init; }
    }

    public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IClientAccountService _accounts;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CreateClientCommandHandler(
            IApplicationDbContext context,
            IClientAccountService accounts,
            ITenantProvider tenant,
            TimeProvider dateTime)
        {
            _context = context;
            _accounts = accounts;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateClientCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Client
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = Trimmed(request.Email),
                BirthDate = request.BirthDate,
                BirthPlace = request.BirthPlace,
                BirthCountryId = request.BirthCountryId,
                CIN = request.CIN,
                CINDeliveranceDate = request.CINDeliveranceDate,
                CINDeliverancePlace = request.CINDeliverancePlace,
                CINDeliveranceCountryId = request.CINDeliveranceCountryId,
                PasseportNumber = request.PasseportNumber,
                PasseportDeliveranceDate = request.PasseportDeliveranceDate,
                PasseportDeliverancePlace = request.PasseportDeliverancePlace,
                PasseportDeliveranceCountryId = request.PasseportDeliveranceCountryId,
                DrivingLicenceNumber = request.DrivingLicenceNumber,
                DrivingLicenceDeliveranceDate = request.DrivingLicenceDeliveranceDate,
                DrivingLicenceDeliverancePlace = request.DrivingLicenceDeliverancePlace,
                DrivingLicenceDeliveranceCountryId = request.DrivingLicenceDeliveranceCountryId,
                Description = request.Description
            };

            // Quota check and insert are atomic under the per-agency write
            // lock; disposing without commit rolls back.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                _context, _tenant, _dateTime, _context.Clients, p => p.MaxClients, "clients", cancellationToken);

            _context.Clients.Add(entity);

            // Saved before provisioning so the client has its key and its
            // stamped AgencyId: the account service logs against them, and
            // creating the Identity user flushes this context anyway.
            await _context.SaveChangesAsync(cancellationToken);

            var account = await _accounts.LinkOrCreateAsync(entity, cancellationToken);

            if (account.Outcome is not ClientAccountOutcome.None)
            {
                // Persists the MarketplaceUserId link the service just set.
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            // Only now: an email cannot be recalled if the commit above had
            // failed (see IClientAccountService).
            await _accounts.SendCredentialsAsync(account, cancellationToken);

            return entity.Id;
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
