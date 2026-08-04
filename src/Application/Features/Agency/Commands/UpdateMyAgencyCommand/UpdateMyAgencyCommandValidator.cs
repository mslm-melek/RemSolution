
using RemSolution.Application.Common.Interfaces;

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

            RuleFor(v => v.ClientReminderDaysBeforeStart)
                .InclusiveBetween(0, 90);

            RuleFor(v => v.ClientReminderDaysBeforeEnd)
                .InclusiveBetween(0, 90);
        }
    }
}
