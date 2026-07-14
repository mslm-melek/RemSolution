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
        // MarketplaceUserId is not editable here (linked once by the Phase 6
        // marketplace flow), and the document image URLs are owned by
        // UploadClientDocumentCommand, which manages the stored files'
        // lifecycle.
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
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

        public UpdateClientCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateClientCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.FirstName = request.FirstName;
            entity.LastName = request.LastName;
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

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
