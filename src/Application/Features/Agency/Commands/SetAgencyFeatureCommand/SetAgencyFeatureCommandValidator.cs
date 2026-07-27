using RemSolution.Domain.Constants;

using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Agency.Commands.SetAgencyFeatureCommand
{
    public class SetAgencyFeatureCommandValidator : AbstractValidator<SetAgencyFeatureCommand>
    {
        public SetAgencyFeatureCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.AgencyId)
                .GreaterThan(0);

            RuleFor(v => v.Feature)
                .Must(f => FeatureFlags.All.Contains(f))
                .WithMessage(_ => localizer["Validation.Feature.Unknown"]);
        }
    }
}
