
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Agency.Models;

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand
{
    public class CreateAgencyCommandValidator : AbstractValidator<CreateAgencyCommand>
    {
        public CreateAgencyCommandValidator(ILocalizer localizer)
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

            RuleForEach(v => v.Branches)
                .SetValidator(new AgencyBranchInputValidator(localizer));

            RuleFor(v => v.Currency)
                .NotEmpty()
                .Length(3).WithMessage(_ => localizer["Validation.Currency.Iso4217"]);

            // Either the admin address or the agency's own satisfies the requirement.
            RuleFor(v => v.AdminEmail)
                .MaximumLength(320)
                .EmailAddress()
                .When(v => !string.IsNullOrWhiteSpace(v.AdminEmail));

            RuleFor(v => v.AdminFullName)
                .MaximumLength(200);

            RuleFor(v => v)
                .Must(v => !string.IsNullOrWhiteSpace(v.ResolveAdminEmail()))
                .WithMessage(_ => localizer["Validation.Agency.AdminEmailRequired"])
                .OverridePropertyName(nameof(CreateAgencyCommand.AdminEmail));

            RuleFor(v => v.CancellationWindowHours)
                .GreaterThanOrEqualTo(0);

            RuleFor(v => v.ReservationExpiryHours)
                .GreaterThan(0);
        }
    }
}
