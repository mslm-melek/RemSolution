
using RemSolution.Application.Common.Interfaces;

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

            RuleFor(v => v.Currency)
                .NotEmpty()
                .Length(3).WithMessage(_ => localizer["Validation.Currency.Iso4217"]);

            RuleFor(v => v.CancellationWindowHours)
                .GreaterThanOrEqualTo(0);

            RuleFor(v => v.ReservationExpiryHours)
                .GreaterThan(0);
        }
    }
}
