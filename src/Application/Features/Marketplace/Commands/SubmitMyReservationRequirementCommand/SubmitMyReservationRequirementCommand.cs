using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.SubmitMyReservationRequirementCommand
{
    // The customer answers one of the agency's asks: a transfer slip, a scan of a
    // licence, an accepted set of terms. A customer has no tenant, so the
    // requirement is loaded cross-tenant and identity is proven by the link that
    // already exists — the reservation's Client row must carry this user's
    // MarketplaceUserId — before acting as the agency for the write.
    //
    // ISensitiveRequest: carries an uploaded document stream.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record SubmitMyReservationRequirementCommand : IRequest, ISensitiveRequest
    {
        public int Id { get; init; }
        public string? Note { get; init; }
        // Absent when the customer is merely accepting terms.
        public string? FileName { get; init; }
        public string? ContentType { get; init; }
        public Stream? Content { get; init; }
    }

    public class SubmitMyReservationRequirementCommandHandler
        : IRequestHandler<SubmitMyReservationRequirementCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public SubmitMyReservationRequirementCommandHandler(
            IApplicationDbContext context, IStoredFileService storedFiles,
            IUser user, TimeProvider dateTime)
        {
            _context = context;
            _storedFiles = storedFiles;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task Handle(
            SubmitMyReservationRequirementCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var owner = await _context.ReservationRequirements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == request.Id
                            && r.Reservation != null
                            && r.Reservation.Client != null
                            && r.Reservation.Client.MarketplaceUserId == userId)
                .Select(r => new { r.Id, r.AgencyId, r.Kind, r.ReservationId, ReservationStatus = r.Reservation!.Status })
                .FirstOrDefaultAsync(cancellationToken);

            // Somebody else's requirement is indistinguishable from a missing one.
            Guard.Against.NotFound(request.Id, owner);

            // A hold that has been cancelled, refused or has lapsed asks nothing
            // of anyone any more.
            if (owner.ReservationStatus is not (ReservationStatus.Confirmed or ReservationStatus.Paid))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This reservation is closed, so there is nothing left to send.")
                });
            }

            using var _ = AmbientTenant.Push(owner.AgencyId);

            var entity = await _context.ReservationRequirements
                .FirstAsync(r => r.Id == owner.Id, cancellationToken);

            int? fileId = null;

            if (request.Content is not null && !string.IsNullOrWhiteSpace(request.FileName))
            {
                // A transfer slip is proof of a movement whichever screen it
                // arrived through; everything else is the requirement's own tag.
                var documentType = owner.Kind is ReservationRequirementKind.Payment
                        or ReservationRequirementKind.Deposit
                    ? DocumentType.PaymentProof
                    : DocumentType.BookingRequirement;

                var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
                var relativePath =
                    $"agencies/{owner.AgencyId}/reservations/{owner.ReservationId}/requirement-{owner.Id}-{Guid.NewGuid():N}{extension}";

                var file = await _storedFiles.CreateAsync(
                    request.Content, request.FileName, request.ContentType ?? "application/octet-stream",
                    documentType, relativePath, cancellationToken);

                // Saved before the FK is set: the file needs its key, and the
                // requirement's own write follows in the same call.
                await _context.SaveChangesAsync(cancellationToken);
                fileId = file.Id;
            }

            entity.Submit(fileId, request.Note, _dateTime.GetUtcNow().UtcDateTime);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.SubmitMyReservationRequirementCommand
{
    public class SubmitMyReservationRequirementCommandValidator
        : AbstractValidator<SubmitMyReservationRequirementCommand>
    {
        public SubmitMyReservationRequirementCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Note)
                .MaximumLength(Domain.Entities.ReservationRequirement.MaxNoteLength);
        }
    }
}
