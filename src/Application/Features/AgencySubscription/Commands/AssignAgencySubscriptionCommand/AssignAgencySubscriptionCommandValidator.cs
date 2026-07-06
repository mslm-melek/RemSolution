namespace RemSolution.Application.Features.AgencySubscription.Commands.AssignAgencySubscriptionCommand
{
    public class AssignAgencySubscriptionCommandValidator : AbstractValidator<AssignAgencySubscriptionCommand>
    {
        public AssignAgencySubscriptionCommandValidator()
        {
            RuleFor(s => s.AgencyId)
                .GreaterThan(0);

            RuleFor(s => s.PlanId)
                .GreaterThan(0);

            RuleFor(s => s.EndDate)
                .GreaterThan(s => s.StartDate)
                .WithMessage("'End Date' must be after 'Start Date'.");
        }
    }
}
