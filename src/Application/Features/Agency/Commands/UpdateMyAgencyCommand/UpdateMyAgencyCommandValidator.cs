
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Agency.Commands.UpdateMyAgencyCommand
{
    public class UpdateMyAgencyCommandValidator : AbstractValidator<UpdateMyAgencyCommand>
    {
        public UpdateMyAgencyCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();

            RuleFor(v => v.Email)
                .MaximumLength(320)
                .EmailAddress()
                .When(v => !string.IsNullOrEmpty(v.Email));

            RuleFor(v => v.PhoneNumber)
                .MaximumLength(50);

            RuleFor(v => v.Address)
                .MaximumLength(500);

            RuleFor(v => v.CountryId)
                .GreaterThan(0);

            RuleFor(v => v.Latitude)
                .InclusiveBetween(-90, 90)
                .When(v => v.Latitude.HasValue);

            RuleFor(v => v.Longitude)
                .InclusiveBetween(-180, 180)
                .When(v => v.Longitude.HasValue);

            RuleFor(v => v)
                .Must(v => v.Latitude.HasValue == v.Longitude.HasValue)
                .WithMessage(_ => localizer["Validation.Coordinates.Together"]);

            RuleFor(v => v.CancellationWindowHours)
                .GreaterThanOrEqualTo(0);

            RuleFor(v => v.ReservationExpiryHours)
                .GreaterThan(0);

            // Lead times: zero is meaningful (no warning window / that reminder
            // off), negative is not. The upper bounds keep a typo from turning
            // "14 days" into a year of alerts about everything at once.
            RuleFor(v => v.ExpenseDueLeadDays)
                .InclusiveBetween(0, 365);

            RuleFor(v => v.ExpenseDueLeadKilometers)
                .InclusiveBetween(0, 100_000);

            RuleFor(v => v.ReservationUpcomingLeadDays)
                .InclusiveBetween(0, 365);

            RuleFor(v => v.ClientDocumentExpiryLeadDays)
                .InclusiveBetween(0, 365);

            RuleFor(v => v.ClientReminderDaysBeforeStart)
                .InclusiveBetween(0, 90);

            RuleFor(v => v.ClientReminderDaysBeforeEnd)
                .InclusiveBetween(0, 90);

            RuleFor(v => v.TaxIdentifier)
                .MaximumLength(40);

            // Zero is a legitimate rate (an exempt agency); the ceiling only
            // catches a rate typed as a fraction the wrong way round.
            RuleFor(v => v.VatRatePercent)
                .InclusiveBetween(0m, 100m);

            RuleFor(v => v.FiscalStampAmount)
                .GreaterThanOrEqualTo(0m);

            // Zero switches the derivation off for that document; the ceiling is
            // only there to catch a date typed into a duration field.
            RuleFor(v => v.CINValidityYears).InclusiveBetween(0, 50);
            RuleFor(v => v.PasseportValidityYears).InclusiveBetween(0, 50);
            RuleFor(v => v.DrivingLicenceValidityYears).InclusiveBetween(0, 50);

            RuleFor(v => v.CancellationFeeMode).IsInEnum();

            RuleFor(v => v.CancellationFeeValue)
                .GreaterThan(0m)
                .When(v => v.CancellationFeeMode != CancellationFeeMode.None)
                .WithMessage(_ => localizer["Validation.Agency.CancellationFeeValue"]);

            RuleFor(v => v.CancellationFeeValue)
                .LessThanOrEqualTo(100m)
                .When(v => v.CancellationFeeMode == CancellationFeeMode.PercentOfPrice)
                .WithMessage(_ => localizer["Validation.Agency.CancellationFeePercent"]);

            // Below the cutoff there is no band left to charge in: cancelling is
            // free right up to the moment it becomes impossible.
            RuleFor(v => v.CancellationFreeHours)
                .InclusiveBetween(0, 8760)
                .GreaterThanOrEqualTo(v => v.CancellationWindowHours)
                .When(v => v.CancellationFeeMode != CancellationFeeMode.None)
                .WithMessage(_ => localizer["Validation.Agency.CancellationFreeHours"]);

            // Zero switches the purge off; the window is in months, so the
            // shortest rule anyone can set still leaves a month. Ten years is
            // longer than any retention rule and catches a year typed in here.
            RuleFor(v => v.PersonalDataRetentionMonths)
                .InclusiveBetween(0, 120);

            // Empty switches the courtesy line off; anything else is an ISO 4217
            // code. Whether a rate is actually quoted for the pair is not checked
            // here — the invoice simply prints one line fewer, and refusing the
            // save would make the setting unreachable until somebody quoted one.
            RuleFor(v => v.InvoiceDisplayCurrency)
                .Length(3)
                .Matches("^[A-Za-z]{3}$")
                .When(v => !string.IsNullOrWhiteSpace(v.InvoiceDisplayCurrency));
        }
    }
}
