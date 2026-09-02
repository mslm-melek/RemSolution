using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Client.Validation
{
    // Formats stay permissive (alphanumeric, bounded length) because clients
    // can hold documents from any country; strict national formats would
    // reject legitimate foreign documents.
    // An expiry date, unlike an issue date, is allowed to be in the past: that is
    // precisely the case worth recording, and refusing it would leave the agency
    // unable to enter the expired licence a client turned up with. What it may not
    // be is before the document was issued.
    public abstract class ClientPayloadValidator<T> : AbstractValidator<T> where T : IClientPayload
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        protected ClientPayloadValidator(IApplicationDbContext context, TimeProvider dateTime, ILocalizer localizer)
        {
            _context = context;
            _dateTime = dateTime;

            RuleFor(c => c.FirstName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(c => c.LastName)
                .NotEmpty()
                .MaximumLength(100);

            // An address that fails here would fail Identity's own check a
            // moment later, deep inside account provisioning, where the error
            // could not be attributed to a field.
            RuleFor(c => c.Email)
                .EmailAddress().WithMessage(_ => localizer["Validation.Client.EmailFormat"])
                .MaximumLength(256).WithMessage(_ => localizer["Validation.Client.EmailTooLong"])
                .When(c => !string.IsNullOrWhiteSpace(c.Email));

            RuleFor(c => c.BirthDate)
                .NotNull().WithMessage(_ => localizer["Validation.Client.BirthDateRequired"])
                .Must(BeInThePast).WithMessage(_ => localizer["Validation.Client.BirthDateInPast"]);

            RuleFor(c => c.BirthPlace)
                .MaximumLength(200);

            RuleFor(c => c.BirthCountryId)
                .MustAsync(CountryExists).WithMessage(_ => localizer["Validation.Client.BirthCountryUnknown"])
                .When(c => c.BirthCountryId.HasValue);

            // CIN block
            RuleFor(c => c.CIN)
                .Matches("^[A-Za-z0-9]{4,20}$").WithMessage(_ => localizer["Validation.Client.CinFormat"])
                .When(c => !string.IsNullOrWhiteSpace(c.CIN));

            RuleFor(c => c.CIN)
                .NotEmpty().WithMessage(_ => localizer["Validation.Client.CinRequiredWithDetails"])
                .When(c => c.CINDeliveranceDate.HasValue
                        || !string.IsNullOrWhiteSpace(c.CINDeliverancePlace)
                        || c.CINDeliveranceCountryId.HasValue);

            RuleFor(c => c.CINDeliveranceDate)
                .Must(NotBeInTheFuture).WithMessage(_ => localizer["Validation.Client.CinIssueDateFuture"])
                .Must(NotBeBeforeBirthDate).WithMessage(_ => localizer["Validation.Client.CinIssueDateBeforeBirth"]);

            RuleFor(c => c.CINDeliverancePlace)
                .MaximumLength(200);

            RuleFor(c => c.CINDeliveranceCountryId)
                .MustAsync(CountryExists).WithMessage(_ => localizer["Validation.Client.CinCountryUnknown"])
                .When(c => c.CINDeliveranceCountryId.HasValue);

            RuleFor(c => c.CINExpiryDate)
                .Must((c, expiry) => NotBeforeIssue(expiry, c.CINDeliveranceDate))
                    .WithMessage(_ => localizer["Validation.Client.ExpiryBeforeIssue"]);

            // Passeport block
            RuleFor(c => c.PasseportNumber)
                .Matches("^[A-Za-z0-9]{5,20}$").WithMessage(_ => localizer["Validation.Client.PasseportFormat"])
                .When(c => !string.IsNullOrWhiteSpace(c.PasseportNumber));

            RuleFor(c => c.PasseportNumber)
                .NotEmpty().WithMessage(_ => localizer["Validation.Client.PasseportRequiredWithDetails"])
                .When(c => c.PasseportDeliveranceDate.HasValue
                        || !string.IsNullOrWhiteSpace(c.PasseportDeliverancePlace)
                        || c.PasseportDeliveranceCountryId.HasValue);

            RuleFor(c => c.PasseportDeliveranceDate)
                .Must(NotBeInTheFuture).WithMessage(_ => localizer["Validation.Client.PasseportIssueDateFuture"])
                .Must(NotBeBeforeBirthDate).WithMessage(_ => localizer["Validation.Client.PasseportIssueDateBeforeBirth"]);

            RuleFor(c => c.PasseportDeliverancePlace)
                .MaximumLength(200);

            RuleFor(c => c.PasseportDeliveranceCountryId)
                .MustAsync(CountryExists).WithMessage(_ => localizer["Validation.Client.PasseportCountryUnknown"])
                .When(c => c.PasseportDeliveranceCountryId.HasValue);

            RuleFor(c => c.PasseportExpiryDate)
                .Must((c, expiry) => NotBeforeIssue(expiry, c.PasseportDeliveranceDate))
                    .WithMessage(_ => localizer["Validation.Client.ExpiryBeforeIssue"]);

            // Driving licence block (dashes, slashes and spaces are common in
            // licence numbers).
            RuleFor(c => c.DrivingLicenceNumber)
                .Matches(@"^[A-Za-z0-9][A-Za-z0-9 /-]{2,28}[A-Za-z0-9]$").WithMessage(_ => localizer["Validation.Client.LicenceFormat"])
                .When(c => !string.IsNullOrWhiteSpace(c.DrivingLicenceNumber));

            RuleFor(c => c.DrivingLicenceNumber)
                .NotEmpty().WithMessage(_ => localizer["Validation.Client.LicenceRequiredWithDetails"])
                .When(c => c.DrivingLicenceDeliveranceDate.HasValue
                        || !string.IsNullOrWhiteSpace(c.DrivingLicenceDeliverancePlace)
                        || c.DrivingLicenceDeliveranceCountryId.HasValue);

            RuleFor(c => c.DrivingLicenceDeliveranceDate)
                .Must(NotBeInTheFuture).WithMessage(_ => localizer["Validation.Client.LicenceIssueDateFuture"])
                .Must(NotBeBeforeBirthDate).WithMessage(_ => localizer["Validation.Client.LicenceIssueDateBeforeBirth"]);

            RuleFor(c => c.DrivingLicenceDeliverancePlace)
                .MaximumLength(200);

            RuleFor(c => c.DrivingLicenceDeliveranceCountryId)
                .MustAsync(CountryExists).WithMessage(_ => localizer["Validation.Client.LicenceCountryUnknown"])
                .When(c => c.DrivingLicenceDeliveranceCountryId.HasValue);

            RuleFor(c => c.DrivingLicenceExpiryDate)
                .Must((c, expiry) => NotBeforeIssue(expiry, c.DrivingLicenceDeliveranceDate))
                    .WithMessage(_ => localizer["Validation.Client.ExpiryBeforeIssue"]);
        }

        private DateTime Today() => _dateTime.GetUtcNow().UtcDateTime.Date;

        private bool BeInThePast(DateTime? date) =>
            date is null || date.Value.Date < Today();

        private bool NotBeInTheFuture(DateTime? date) =>
            date is null || date.Value.Date <= Today();

        // A document valid before it was issued is a typo, whichever field the
        // typo is in. Either date being absent says nothing, so it passes.
        private static bool NotBeforeIssue(DateTime? expiry, DateTime? issued) =>
            expiry is null || issued is null || expiry.Value.Date > issued.Value.Date;

        private static bool NotBeBeforeBirthDate(T command, DateTime? issueDate) =>
            issueDate is null || command.BirthDate is null || issueDate.Value.Date >= command.BirthDate.Value.Date;

        private async Task<bool> CountryExists(int? countryId, CancellationToken cancellationToken) =>
            await _context.Countries.AnyAsync(co => co.Id == countryId, cancellationToken);
    }
}
