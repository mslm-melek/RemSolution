using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.SubscriptionPlan.Commands.UpdateSubscriptionPlanCommand
{
    public class UpdateSubscriptionPlanCommandValidator : AbstractValidator<UpdateSubscriptionPlanCommand>
    {
        public UpdateSubscriptionPlanCommandValidator()
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
                .WithMessage("'{PropertyValue}' is not a known feature.");
        }
    }
}
