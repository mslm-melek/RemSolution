using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using FluentValidation.Results;
using RentingEntity = RemSolution.Domain.Entities.Renting;

namespace RemSolution.Application.Features.Renting.Commands.CreateRentingCommand
{
    // Creates a renting, optionally creating its client and issuing its paperwork
    // in the SAME unit of work — the counter flow, where the agent has a walk-in
    // customer in front of them and wants a signed contract at the end of it.
    //
    // ISensitiveRequest: NewClient carries identity-document numbers, so the
    // pipeline behaviours must never destructure this request into logs.
    [Authorize(Policy = Permissions.RentingCreate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record CreateRentingCommand : IRequest<int>, ISensitiveRequest
    {
        public int CarId { get; init; }

        // Exactly one of ClientId / NewClient is supplied (see the validator).
        public int? ClientId { get; init; }
        public NewRentingClient? NewClient { get; init; }

        public int? SecondClientId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int? StartMileage { get; init; }
        public string? Notes { get; init; }

        // Paperwork to issue alongside the renting. Each is gated on its own
        // permission AND feature, checked in the handler — asking for a document
        // the agency does not have is a 403, not a silently skipped document.
        public bool GenerateContract { get; init; }
        public bool GenerateFacture { get; init; }

        // Which template each document uses; null takes the agency's default and
        // then the shipped example (see IRentalDocumentService).
        public int? ContractTemplateId { get; init; }
        public int? FactureTemplateId { get; init; }

        // Values for the templates' ask-each-time placeholders, keyed by
        // placeholder name. One bag for both documents rather than two: the SPA
        // prompts for the union of what they need, and a name shared by both
        // templates means the same thing on both.
        public Dictionary<string, string>? DocumentValues { get; init; }
    }

    public class CreateRentingCommandHandler : IRequestHandler<CreateRentingCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;
        private readonly IRentalDocumentService _documents;
        private readonly IStoredFileService _storedFiles;
        private readonly IIdentityService _identityService;
        private readonly IUser _user;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CreateRentingCommandHandler(
            IApplicationDbContext context,
            IPricingService pricing,
            IAvailabilityChecker availability,
            IRentalDocumentService documents,
            IStoredFileService storedFiles,
            IIdentityService identityService,
            IUser user,
            ITenantProvider tenant,
            TimeProvider dateTime)
        {
            _context = context;
            _pricing = pricing;
            _availability = availability;
            _documents = documents;
            _storedFiles = storedFiles;
            _identityService = identityService;
            _user = user;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateRentingCommand request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            if (car.Status != CarStatus.Active)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car is not Active and cannot be booked.")
                });
            }

            if (car.DailyRate is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before booking it.")
                });
            }

            // Snapshot the agreed price once, at creation (see IPricingService).
            var price = _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);

            // Rendered PDFs are written to storage before their rows commit, so a
            // rollback has to take the bytes with it (same ordering as the
            // car-image upload path).
            var generatedFiles = new List<StoredFile>();

            // Everything below is atomic under the per-agency write lock: the
            // availability check, the client quota, the document numbering and
            // every insert. A booking conflict therefore also rolls back a client
            // created inline — no orphan record from a failed booking.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            try
            {
                await _availability.EnsureCarAvailableAsync(
                    request.CarId, request.StartDate, request.EndDate, null, null, cancellationToken);

                var clientId = await ResolveClientIdAsync(request, cancellationToken);

                var entity = new RentingEntity
                {
                    CarId = request.CarId,
                    ClientId = clientId,
                    SecondClientId = request.SecondClientId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    StartMileage = request.StartMileage,
                    Price = price,
                    RentingState = RentingState.NotYet,
                    Notes = request.Notes,
                };

                _context.Rentings.Add(entity);

                // The documents reference the renting by id and render its data,
                // so the renting has to exist first. Still the same transaction:
                // this save is not yet durable.
                await _context.SaveChangesAsync(cancellationToken);

                if (request.GenerateContract)
                {
                    await EnsureAsync(Permissions.ContractGenerate, FeatureFlags.Contracts, cancellationToken);

                    var contract = await _documents.GenerateContractAsync(
                        new RentalDocumentRequest(
                            entity.Id, request.ContractTemplateId, request.DocumentValues),
                        cancellationToken);

                    TrackFile(generatedFiles, contract.DocumentFile);
                }

                if (request.GenerateFacture)
                {
                    await EnsureAsync(Permissions.FactureGenerate, FeatureFlags.Factures, cancellationToken);

                    var facture = await _documents.GenerateFactureAsync(
                        new RentalDocumentRequest(
                            entity.Id, request.FactureTemplateId, request.DocumentValues),
                        cancellationToken);

                    TrackFile(generatedFiles, facture.DocumentFile);
                }

                if (request.GenerateContract || request.GenerateFacture)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                return entity.Id;
            }
            catch
            {
                foreach (var file in generatedFiles)
                {
                    await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);
                }

                throw;
            }
        }

        // Returns the client the renting attaches to: the one that was picked, or
        // one created from the inline payload.
        //
        // Creating inline applies the same three gates the standalone
        // CreateClientCommand does — Client.Create + the Clients feature, the
        // plan's MaxClients quota — plus the CIN/passport dedup rule from
        // ConvertReservationCommand: if the agency already holds a client with
        // that document, the renting links to THEM rather than adding a duplicate
        // for the same person.
        private async Task<int> ResolveClientIdAsync(CreateRentingCommand request, CancellationToken cancellationToken)
        {
            if (request.NewClient is not NewRentingClient payload)
            {
                // Validated as present when NewClient is absent.
                return request.ClientId!.Value;
            }

            await EnsureAsync(Permissions.ClientCreate, FeatureFlags.Clients, cancellationToken);

            var cin = Trimmed(payload.CIN);
            var passport = Trimmed(payload.PasseportNumber);

            if (cin is not null || passport is not null)
            {
                var existing = await _context.Clients
                    .FirstOrDefaultAsync(
                        c => (cin != null && c.CIN == cin) || (passport != null && c.PasseportNumber == passport),
                        cancellationToken);

                if (existing is not null)
                {
                    // Fill blanks only: the stored record is the agency's own and
                    // must not be overwritten by whatever was typed at the counter.
                    Enrich(existing, payload);
                    return existing.Id;
                }
            }

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                _context, _tenant, _dateTime, _context.Clients, p => p.MaxClients, "clients", cancellationToken);

            var client = new Domain.Entities.Client
            {
                FirstName = payload.FirstName,
                LastName = payload.LastName,
                BirthDate = payload.BirthDate,
                BirthPlace = payload.BirthPlace,
                BirthCountryId = payload.BirthCountryId,
                CIN = cin,
                CINDeliveranceDate = payload.CINDeliveranceDate,
                CINDeliverancePlace = payload.CINDeliverancePlace,
                CINDeliveranceCountryId = payload.CINDeliveranceCountryId,
                PasseportNumber = passport,
                PasseportDeliveranceDate = payload.PasseportDeliveranceDate,
                PasseportDeliverancePlace = payload.PasseportDeliverancePlace,
                PasseportDeliveranceCountryId = payload.PasseportDeliveranceCountryId,
                DrivingLicenceNumber = Trimmed(payload.DrivingLicenceNumber),
                DrivingLicenceDeliveranceDate = payload.DrivingLicenceDeliveranceDate,
                DrivingLicenceDeliverancePlace = payload.DrivingLicenceDeliverancePlace,
                DrivingLicenceDeliveranceCountryId = payload.DrivingLicenceDeliveranceCountryId,
                Description = payload.Description
                // AgencyId is stamped by TenantEntityInterceptor on insert.
            };

            _context.Clients.Add(client);

            // The renting needs the client's key, and both are inside the same
            // transaction, so this save is not yet durable either.
            await _context.SaveChangesAsync(cancellationToken);

            return client.Id;
        }

        private static void Enrich(Domain.Entities.Client client, NewRentingClient payload)
        {
            if (string.IsNullOrWhiteSpace(client.CIN))
            {
                client.CIN = Trimmed(payload.CIN);
            }

            if (string.IsNullOrWhiteSpace(client.PasseportNumber))
            {
                client.PasseportNumber = Trimmed(payload.PasseportNumber);
            }

            if (string.IsNullOrWhiteSpace(client.DrivingLicenceNumber))
            {
                client.DrivingLicenceNumber = Trimmed(payload.DrivingLicenceNumber);
            }
        }

        private Task EnsureAsync(string permission, string feature, CancellationToken cancellationToken) =>
            Entitlements.EnsureAsync(
                _user, _identityService, _context, _tenant, _dateTime, permission, feature, cancellationToken);

        private static void TrackFile(List<StoredFile> files, StoredFile? file)
        {
            if (file is not null)
            {
                files.Add(file);
            }
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

namespace RemSolution.Application.Features.Renting.Commands.CreateRentingCommand
{
    public class CreateRentingCommandValidator : AbstractValidator<CreateRentingCommand>
    {
        public CreateRentingCommandValidator(
            IApplicationDbContext context, TimeProvider dateTime, ILocalizer localizer)
        {
            RuleFor(v => v.CarId).GreaterThan(0);

            // Exactly one client source. Reported on ClientId so the SPA's
            // existing/new toggle has a field to highlight.
            RuleFor(v => v.ClientId)
                .Must((command, clientId) => (clientId > 0) ^ (command.NewClient is not null))
                    .WithMessage(_ => localizer["Validation.Renting.ClientOrNewClient"]);

            RuleFor(v => v.NewClient!)
                .SetValidator(new NewRentingClientValidator(context, dateTime, localizer))
                .When(v => v.NewClient is not null);

            RuleFor(v => v.SecondClientId)
                .GreaterThan(0).When(v => v.SecondClientId.HasValue)
                .NotEqual(v => v.ClientId).When(v => v.SecondClientId.HasValue)
                    .WithMessage(_ => localizer["Validation.Renting.SecondDriverDistinct"]);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);
            RuleFor(v => v.StartMileage)
                .GreaterThanOrEqualTo(0).When(v => v.StartMileage.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
