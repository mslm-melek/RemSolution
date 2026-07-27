using RemSolution.Domain.Constants;

using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.CreateSubscriptionPlanCommand
{
    public class CreateSubscriptionPlanCommandValidator : AbstractValidator<CreateSubscriptionPlanCommand>
    {
        public CreateSubscriptionPlanCommandValidator(ILocalizer localizer)
        {
            RuleFor(p => p.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(p => p.MaxCars)
                .GreaterThan(0);

            RuleFor(p => p.MaxClients)
                .GreaterThan(0);

            RuleFor(p => p.MaxUsers)
                .GreaterThan(0);

            RuleFor(p => p.Price)
                .GreaterThanOrEqualTo(0);

            RuleForEach(p => p.Features)
                .Must(f => FeatureFlags.All.Contains(f))
                .WithMessage(_ => localizer["Validation.Feature.Unknown"]);
        }
    }
}
