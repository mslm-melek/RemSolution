using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.EraseClientPersonalDataCommand
{
    /// <summary>
    /// Erases a client's personal data for good — the right-to-erasure request,
    /// answered. Irreversible: the identity details go, the document scans are
    /// deleted from storage, and the audit payloads about the client are blanked.
    /// The hires, payments and invoices stay (see <c>ClientPersonalDataErasure</c>).
    /// <para>
    /// NOT <c>[Auditable]</c>, and that is the point: the audit interceptor
    /// captures before/after state as JSON, so marking this command auditable
    /// would copy the passport number being erased into a fresh audit row. The
    /// erasure records itself instead, through
    /// <see cref="IPersonalDataErasureStore"/>, with no payload.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.ClientErase)]
    [RequiresFeature(FeatureFlags.Clients)]
    public record EraseClientPersonalDataCommand(int Id, string? Reason = null) : IRequest;

    public class EraseClientPersonalDataCommandValidator
        : AbstractValidator<EraseClientPersonalDataCommand>
    {
        public EraseClientPersonalDataCommandValidator()
        {
            RuleFor(c => c.Id).GreaterThan(0);
            RuleFor(c => c.Reason).MaximumLength(500);
        }
    }

    public class EraseClientPersonalDataCommandHandler
        : IRequestHandler<EraseClientPersonalDataCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPersonalDataErasureStore _store;
        private readonly IStoredFileService _storedFiles;
        private readonly ILocalizer _localizer;
        private readonly TimeProvider _dateTime;

        public EraseClientPersonalDataCommandHandler(
            IApplicationDbContext context,
            IPersonalDataErasureStore store,
            IStoredFileService storedFiles,
            ILocalizer localizer,
            TimeProvider dateTime)
        {
            _context = context;
            _store = store;
            _storedFiles = storedFiles;
            _localizer = localizer;
            _dateTime = dateTime;
        }

        public async Task Handle(
            EraseClientPersonalDataCommand request, CancellationToken cancellationToken)
        {
            // Through the store, not the context: a client archived a year ago is
            // hidden from every ordinary read, and is exactly the one an erasure
            // request is most likely to be about.
            var client = await _store.FindClientAsync(request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, client);

            // Already done. Silently succeeding is the honest answer to "erase
            // this client" on a client that holds nothing left to erase, and it
            // keeps a retried request from writing a second erasure row.
            if (client.PersonalDataErasedAt is not null) return;

            if (await ClientPersonalDataErasure.HasLiveBookingAsync(
                    _context, client.Id, cancellationToken))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(
                        nameof(request.Id), _localizer["Validation.Client.EraseLiveBooking"])
                });
            }

            await ClientPersonalDataErasure.EraseAsync(
                _context, _storedFiles, _store, client,
                _dateTime.GetUtcNow(), request.Reason, cancellationToken);
        }
    }
}
