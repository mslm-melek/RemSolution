using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Client.Validation;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.UpdateClientCommand
{
    // Auditable: rewrites every identity field (CIN, passeport, licence),
    // so the before/after trail is mandatory for fraud investigations.
    // ISensitiveRequest: those same fields must never reach the logs.
    [Authorize(Policy = Permissions.ClientUpdate)]
    [RequiresFeature(FeatureFlags.Clients)]
    [Auditable("UpdateClient", "Client")]
    public record UpdateClientCommand : IRequest, IClientPayload, ISensitiveRequest
    {
        public int Id { get; init; }
        // The row version the client last read; the update targets exactly that
        // version so a concurrent change surfaces as a 409 (see P.8).
        public byte[]? RowVersion { get; init; }
        // MarketplaceUserId is not editable here — it is a link the account
        // service owns, and the handler below explains why editing Email does
        // not move it. The document image URLs are owned by
        // UploadClientDocumentCommand, which manages the stored files'
        // lifecycle.
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
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
        public string? Description { get; init; }
    }

    public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IClientAccountService _accounts;

        public UpdateClientCommandHandler(IApplicationDbContext context, IClientAccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        public async Task Handle(UpdateClientCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.FirstName = request.FirstName;
            entity.LastName = request.LastName;
            entity.Email = Trimmed(request.Email);
            entity.BirthDate = request.BirthDate;
            entity.BirthPlace = request.BirthPlace;
            entity.BirthCountryId = request.BirthCountryId;
            entity.CIN = request.CIN;
            entity.CINDeliveranceDate = request.CINDeliveranceDate;
            entity.CINDeliverancePlace = request.CINDeliverancePlace;
            entity.CINDeliveranceCountryId = request.CINDeliveranceCountryId;
            entity.PasseportNumber = request.PasseportNumber;
            entity.PasseportDeliveranceDate = request.PasseportDeliveranceDate;
            entity.PasseportDeliverancePlace = request.PasseportDeliverancePlace;
            entity.PasseportDeliveranceCountryId = request.PasseportDeliveranceCountryId;
            entity.DrivingLicenceNumber = request.DrivingLicenceNumber;
            entity.DrivingLicenceDeliveranceDate = request.DrivingLicenceDeliveranceDate;
            entity.DrivingLicenceDeliverancePlace = request.DrivingLicenceDeliverancePlace;
            entity.DrivingLicenceDeliveranceCountryId = request.DrivingLicenceDeliveranceCountryId;
            entity.Description = request.Description;

            // Adding an email to a client who never had one gives them a login;
            // this is the same provisioning the create path does, so an agency
            // that fills the field in later gets the same result as one that
            // filled it in from the start.
            //
            // Two things it deliberately does NOT do (both enforced inside the
            // service): re-point an already-linked client at a different
            // account when the address is edited, and unlink when the address
            // is cleared. The account outlives the contact field — it owns the
            // customer's bookings, documents and chat threads.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            var account = await _accounts.LinkOrCreateAsync(entity, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await _accounts.SendCredentialsAsync(account, cancellationToken);
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
