using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
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

        // The second driver, who is just as likely to be a walk-in as the renter —
        // a couple at the counter, one of whom has never rented here. At most one
        // of the two is supplied; neither means no second driver.
        public int? SecondClientId { get; init; }
        public NewRentingClient? SecondNewClient { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int? StartMileage { get; init; }

        /// <summary>
        /// The agreed price for the whole period, when it is not the automatic one.
        /// Null — the normal case — prices the period through IPricingService.
        /// <para>
        /// Rentals get negotiated at the counter (a returning customer, a weekly
        /// deal, a car whose rate has not been set yet), so the quote is a default
        /// rather than a rule. Whichever way the figure is arrived at it is then
        /// snapshotted on the renting exactly the same way, and the paperwork
        /// renders it — there is no "manual price" mode downstream.
        /// </para>
        /// Taken to be in the car's currency, falling back to the agency's; an
        /// agency has one currency, so there is nothing to choose.
        /// </summary>
        public decimal? PriceOverride { get; init; }

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
        private readonly IAgencySettingsProvider _settings;
        private readonly IAvailabilityChecker _availability;
        private readonly IRentalDocumentService _documents;
        private readonly IStoredFileService _storedFiles;
        private readonly IClientAccountService _accounts;
        private readonly IIdentityService _identityService;
        private readonly IUser _user;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CreateRentingCommandHandler(
            IApplicationDbContext context,
            IPricingService pricing,
            IAgencySettingsProvider settings,
            IAvailabilityChecker availability,
            IRentalDocumentService documents,
            IStoredFileService storedFiles,
            IClientAccountService accounts,
            IIdentityService identityService,
            IUser user,
            ITenantProvider tenant,
            TimeProvider dateTime)
        {
            _context = context;
            _pricing = pricing;
            _settings = settings;
            _availability = availability;
            _documents = documents;
            _storedFiles = storedFiles;
            _accounts = accounts;
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

            // Only the automatic quote needs a rate to work from. An unpriced car
            // can still be booked at a price the agent types in — which is the
            // realistic order of events for a car that has just arrived.
            if (car.DailyRate is null && request.PriceOverride is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before booking it, " +
                        "or agree a price for this renting.")
                });
            }

            // Snapshot the agreed price once, at creation (see IPricingService) —
            // the negotiated figure when there is one, the quote otherwise.
            var price = request.PriceOverride is decimal agreed
                ? Money.Of(agreed, car.DailyRate?.Currency
                    ?? (await _settings.GetAsync(car.AgencyId, cancellationToken)).CurrencyCode).Round()
                : _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);

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

                var clients = new RentingClientContext(
                    _context, _user, _identityService, _tenant, _dateTime);

                var client = await RentingClients.ResolveAsync(
                    clients, request.ClientId, request.NewClient, cancellationToken);

                var secondClient = await RentingClients.ResolveSecondDriverAsync(
                    clients, client, request.SecondClientId, request.SecondNewClient, cancellationToken);

                var entity = new RentingEntity
                {
                    CarId = request.CarId,
                    ClientId = client.Id,
                    SecondClientId = secondClient?.Id,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    StartMileage = request.StartMileage,
                    Price = price,
                    RentingState = RentingState.NotYet,
                    Notes = request.Notes,
                };

                _context.Rentings.Add(entity);

                // A pickup reading typed at the counter is a reading off this car,
                // so the car's odometer follows it (see Car.RecordOdometer) — the
                // next booking then offers the mileage this hire started from
                // rather than a figure from before it.
                car.RecordOdometer(entity.StartMileage);

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

                // The customer now has something to look at: give them the login
                // to look at it with. Runs for the picked client as well as the
                // inline one — an agency that only added an email to an existing
                // client's record last week should not have to re-enter it here.
                var account = await _accounts.LinkOrCreateAsync(client, cancellationToken);

                if (account.Outcome is not ClientAccountOutcome.None)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                // Outside the transaction and outside the catch below: the
                // booking is real now, and an unreachable mail server must not
                // undo it (see IClientAccountService).
                await _accounts.SendCredentialsAsync(account, cancellationToken);

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

            // At most one second-driver source. Unlike the renter, none is valid.
            RuleFor(v => v.SecondClientId)
                .Must((command, secondClientId) => secondClientId is null || command.SecondNewClient is null)
                    .WithMessage(_ => localizer["Validation.Renting.SecondDriverOneSource"]);

            RuleFor(v => v.SecondClientId)
                .GreaterThan(0).When(v => v.SecondClientId.HasValue)
                .NotEqual(v => v.ClientId).When(v => v.SecondClientId.HasValue)
                    .WithMessage(_ => localizer["Validation.Renting.SecondDriverDistinct"]);

            // The same identity rules as the renter's payload; the "not the same
            // person" rule needs both rows and so lives in the handler (see
            // RentingClients.ResolveSecondDriverAsync).
            RuleFor(v => v.SecondNewClient!)
                .SetValidator(new NewRentingClientValidator(context, dateTime, localizer))
                .When(v => v.SecondNewClient is not null);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);
            RuleFor(v => v.StartMileage)
                .GreaterThanOrEqualTo(0).When(v => v.StartMileage.HasValue);
            // Zero is allowed on purpose: a courtesy car is a real booking at no
            // charge, and refusing it would push the agent to invent a price.
            RuleFor(v => v.PriceOverride)
                .GreaterThanOrEqualTo(0).When(v => v.PriceOverride.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
