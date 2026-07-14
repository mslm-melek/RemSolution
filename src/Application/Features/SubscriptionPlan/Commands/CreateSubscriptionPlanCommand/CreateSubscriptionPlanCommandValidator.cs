namespace RemSolution.Application.Features.SubscriptionPlan.Commands.CreateSubscriptionPlanCommand
{
    public class CreateSubscriptionPlanCommandValidator : AbstractValidator<CreateSubscriptionPlanCommand>
    {
        public CreateSubscriptionPlanCommandValidator()
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
        }
    }
}
