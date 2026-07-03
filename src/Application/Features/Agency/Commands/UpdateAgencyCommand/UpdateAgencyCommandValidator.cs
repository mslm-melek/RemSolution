
namespace RemSolution.Application.Features.Agency.Commands.UpdateAgencyCommand
{
    public class UpdateAgencyCommandValidator : AbstractValidator<UpdateAgencyCommand>
    {
        public UpdateAgencyCommandValidator()
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
                .WithMessage("Latitude and Longitude must be provided together.");
        }
    }
}
