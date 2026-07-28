using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Renting.Commands.ChangeRentingEndDateCommand
{
    // The client wants the car for longer — or brings it back early. Both are the
    // same conversation at the desk, so both are the same command: it moves the
    // end date, re-prices, and (only if asked) issues a fresh contract for the new
    // period.
    //
    // Why this exists next to UpdateRentingCommand: that one is the full edit
    // form and RE-QUOTES the whole period from the car's current rate whenever a
    // date moves. For an extension that is wrong — it would silently re-price the
    // days the client already agreed to if the car's rate has changed since. Here
    // the agreed part is carried over and only the difference is priced (see
    // IPricingService.RepriceForNewEndDate).
    //
    // The paperwork is the agent's choice, not a side effect:
    //   RegenerateContract = true  → a new numbered contract for the new period;
    //                                the copy already signed stays retrievable
    //                                (documents are append-only, see Contract).
    //   RegenerateContract = false → the renting changes, the existing contract is
    //                                left exactly as it is.
    //
    // No RentingHistory row is written: that entity records the snapshot of a
    // FINISHED period (see RentingCompletedEvent), not an amendment to a live one.
    [Authorize(Policy = Permissions.RentingUpdate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    [Auditable("ChangeRentingEndDate", "Renting")]
    public record ChangeRentingEndDateCommand : IRequest
    {
        public int Id { get; init; }

        // The row version the client last read; a concurrent change surfaces as
        // a 409 (see P.8).
        public byte[]? RowVersion { get; init; }

        /// <summary>The new end date. May be later (extension) or earlier (early return).</summary>
        public DateTime EndDate { get; init; }

        /// <summary>
        /// Issue a new numbered contract covering the new period. Gated on
        /// Contract.Generate AND the Contracts feature, checked in the handler —
        /// asking for a document the agency does not have is a 403, not a silently
        /// skipped document.
        /// </summary>
        public bool RegenerateContract { get; init; }

        /// <summary>
        /// Template for the regenerated contract; null takes the agency's default
        /// and then the shipped example (see IRentalDocumentService).
        /// </summary>
        public int? ContractTemplateId { get; init; }

        /// <summary>
        /// Values for the template's ask-each-time placeholders, keyed by
        /// placeholder name (see GetDocumentPromptQuery for what to ask).
        /// </summary>
        public Dictionary<string, string>? DocumentValues { get; init; }
    }

    public class ChangeRentingEndDateCommandHandler : IRequestHandler<ChangeRentingEndDateCommand>
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

        public ChangeRentingEndDateCommandHandler(
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

        public async Task Handle(ChangeRentingEndDateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.RentingState is RentingState.Done or RentingState.Cancelled)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "A completed or cancelled renting can no longer be changed.")
                });
            }

            if (entity.CarId is not int carId || entity.StartDate is not DateTime startDate)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "The renting is missing a car or a start date; fix it in the edit form first.")
                });
            }

            if (request.EndDate <= startDate)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.EndDate),
                        "The end date must be after the start date.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            var car = await _context.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);
            Guard.Against.NotFound(carId, car);

            var generatedFiles = new List<StoredFile>();

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            // Held for the whole unit of work: the availability check must not
            // race another booking, and contract numbering is MAX + 1.
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            try
            {
                // Extending can collide with a later booking of the same car;
                // shortening frees time and never can. Either way the rule is the
                // same overlap check, ignoring this renting itself.
                await _availability.EnsureCarAvailableAsync(
                    carId, startDate, request.EndDate,
                    excludeRentingId: entity.Id, excludeReservationId: null, cancellationToken);

                var originalEnd = entity.EndDate;

                // With no agreed price to preserve there is nothing to carry over,
                // so the new period is quoted from scratch.
                entity.Price = entity.Price is { } agreedPrice && originalEnd is DateTime previousEnd
                    ? _pricing.RepriceForNewEndDate(car, agreedPrice, startDate, previousEnd, request.EndDate)
                    : _pricing.CalculateRentalPrice(car, startDate, request.EndDate);

                entity.EndDate = request.EndDate;

                if (request.RegenerateContract)
                {
                    await EnsureAsync(Permissions.ContractGenerate, FeatureFlags.Contracts, cancellationToken);

                    // Saved first: the renderer reads the renting's own row, so the
                    // new dates and price have to be visible to it. Still the same
                    // transaction — this save is not yet durable.
                    await _context.SaveChangesAsync(cancellationToken);

                    var contract = await _documents.GenerateContractAsync(
                        new RentalDocumentRequest(
                            entity.Id, request.ContractTemplateId, request.DocumentValues),
                        cancellationToken);

                    if (contract.DocumentFile is { } file)
                    {
                        generatedFiles.Add(file);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                // The PDF reaches storage before its row is committed, so a
                // rollback would otherwise leave the bytes orphaned (same ordering
                // as the create-renting and car-image paths).
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
    }
}

namespace RemSolution.Application.Features.Renting.Commands.ChangeRentingEndDateCommand
{
    public class ChangeRentingEndDateCommandValidator : AbstractValidator<ChangeRentingEndDateCommand>
    {
        public ChangeRentingEndDateCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.EndDate).NotEmpty();
            RuleFor(v => v.ContractTemplateId)
                .GreaterThan(0).When(v => v.ContractTemplateId.HasValue);
        }
    }
}
