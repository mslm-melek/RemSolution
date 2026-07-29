
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyBranchCommand
{
    public class CreateAgencyBranchCommandValidator : AbstractValidator<CreateAgencyBranchCommand>
    {
        public CreateAgencyBranchCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.AgencyId)
                .GreaterThan(0);

            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();

            RuleFor(v => v.CountryId)
                .GreaterThan(0);

            RuleFor(v => v.Address)
                .MaximumLength(500);

            RuleFor(v => v.Latitude)
                .InclusiveBetween(-90, 90)
                .When(v => v.Latitude.HasValue);

            RuleFor(v => v.Longitude)
                .InclusiveBetween(-180, 180)
                .When(v => v.Longitude.HasValue);

            RuleFor(v => v)
                .Must(v => v.Latitude.HasValue == v.Longitude.HasValue)
                .WithMessage(_ => localizer["Validation.Coordinates.Together"]);
        }
    }
}
