using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Client.Validation
{
    // Formats stay permissive (alphanumeric, bounded length) because clients
    // can hold documents from any country; strict national formats would
    // reject legitimate foreign documents.
    // The entity has no expiry columns yet, so only issue-date rules apply.
    public abstract class ClientPayloadValidator<T> : AbstractValidator<T> where T : IClientPayload
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        protected ClientPayloadValidator(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;

            RuleFor(c => c.FirstName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(c => c.LastName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(c => c.BirthDate)
                .NotNull().WithMessage("Birth date is required.")
                .Must(BeInThePast).WithMessage("Birth date must be in the past.");

            RuleFor(c => c.BirthPlace)
                .MaximumLength(200);

            RuleFor(c => c.BirthCountryId)
                .MustAsync(CountryExists).WithMessage("Birth country does not exist.")
                .When(c => c.BirthCountryId.HasValue);

            // CIN block
            RuleFor(c => c.CIN)
                .Matches("^[A-Za-z0-9]{4,20}$").WithMessage("CIN must be 4 to 20 letters or digits.")
                .When(c => !string.IsNullOrWhiteSpace(c.CIN));

            RuleFor(c => c.CIN)
                .NotEmpty().WithMessage("CIN number is required when CIN issue details are provided.")
                .When(c => c.CINDeliveranceDate.HasValue
                        || !string.IsNullOrWhiteSpace(c.CINDeliverancePlace)
                        || c.CINDeliveranceCountryId.HasValue);

            RuleFor(c => c.CINDeliveranceDate)
                .Must(NotBeInTheFuture).WithMessage("CIN issue date cannot be in the future.")
                .Must(NotBeBeforeBirthDate).WithMessage("CIN issue date cannot be before the birth date.");

            RuleFor(c => c.CINDeliverancePlace)
                .MaximumLength(200);

            RuleFor(c => c.CINDeliveranceCountryId)
                .MustAsync(CountryExists).WithMessage("CIN issue country does not exist.")
                .When(c => c.CINDeliveranceCountryId.HasValue);

            // Passeport block
            RuleFor(c => c.PasseportNumber)
                .Matches("^[A-Za-z0-9]{5,20}$").WithMessage("Passeport number must be 5 to 20 letters or digits.")
                .When(c => !string.IsNullOrWhiteSpace(c.PasseportNumber));

            RuleFor(c => c.PasseportNumber)
                .NotEmpty().WithMessage("Passeport number is required when passeport issue details are provided.")
                .When(c => c.PasseportDeliveranceDate.HasValue
                        || !string.IsNullOrWhiteSpace(c.PasseportDeliverancePlace)
                        || c.PasseportDeliveranceCountryId.HasValue);

            RuleFor(c => c.PasseportDeliveranceDate)
                .Must(NotBeInTheFuture).WithMessage("Passeport issue date cannot be in the future.")
                .Must(NotBeBeforeBirthDate).WithMessage("Passeport issue date cannot be before the birth date.");

            RuleFor(c => c.PasseportDeliverancePlace)
                .MaximumLength(200);

            RuleFor(c => c.PasseportDeliveranceCountryId)
                .MustAsync(CountryExists).WithMessage("Passeport issue country does not exist.")
                .When(c => c.PasseportDeliveranceCountryId.HasValue);

            // Driving licence block (dashes, slashes and spaces are common in
            // licence numbers).
            RuleFor(c => c.DrivingLicenceNumber)
                .Matches(@"^[A-Za-z0-9][A-Za-z0-9 /-]{2,28}[A-Za-z0-9]$").WithMessage("Driving licence number must be 4 to 30 characters (letters, digits, dashes, slashes or spaces).")
                .When(c => !string.IsNullOrWhiteSpace(c.DrivingLicenceNumber));

            RuleFor(c => c.DrivingLicenceNumber)
                .NotEmpty().WithMessage("Driving licence number is required when driving licence issue details are provided.")
                .When(c => c.DrivingLicenceDeliveranceDate.HasValue
                        || !string.IsNullOrWhiteSpace(c.DrivingLicenceDeliverancePlace)
                        || c.DrivingLicenceDeliveranceCountryId.HasValue);

            RuleFor(c => c.DrivingLicenceDeliveranceDate)
                .Must(NotBeInTheFuture).WithMessage("Driving licence issue date cannot be in the future.")
                .Must(NotBeBeforeBirthDate).WithMessage("Driving licence issue date cannot be before the birth date.");

            RuleFor(c => c.DrivingLicenceDeliverancePlace)
                .MaximumLength(200);

            RuleFor(c => c.DrivingLicenceDeliveranceCountryId)
                .MustAsync(CountryExists).WithMessage("Driving licence issue country does not exist.")
                .When(c => c.DrivingLicenceDeliveranceCountryId.HasValue);
        }

        private DateTime Today() => _dateTime.GetUtcNow().UtcDateTime.Date;

        private bool BeInThePast(DateTime? date) =>
            date is null || date.Value.Date < Today();

        private bool NotBeInTheFuture(DateTime? date) =>
            date is null || date.Value.Date <= Today();

        private static bool NotBeBeforeBirthDate(T command, DateTime? issueDate) =>
            issueDate is null || command.BirthDate is null || issueDate.Value.Date >= command.BirthDate.Value.Date;

        private async Task<bool> CountryExists(int? countryId, CancellationToken cancellationToken) =>
            await _context.Countries.AnyAsync(co => co.Id == countryId, cancellationToken);
    }
}
