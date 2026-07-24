using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.SetAgencyFeatureCommand
{
    public class SetAgencyFeatureCommandValidator : AbstractValidator<SetAgencyFeatureCommand>
    {
        public SetAgencyFeatureCommandValidator()
        {
            RuleFor(v => v.AgencyId)
                .GreaterThan(0);

            RuleFor(v => v.Feature)
                .Must(f => FeatureFlags.All.Contains(f))
                .WithMessage("'{PropertyValue}' is not a known feature.");
        }
    }
}
