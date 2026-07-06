namespace RemSolution.Application.Features.AgencySubscription.Commands.UpdateAgencySubscriptionCommand
{
    public class UpdateAgencySubscriptionCommandValidator : AbstractValidator<UpdateAgencySubscriptionCommand>
    {
        public UpdateAgencySubscriptionCommandValidator()
        {
            RuleFor(s => s.Id)
                .GreaterThan(0);

            RuleFor(s => s.Status)
                .IsInEnum();

            RuleFor(s => s.EndDate)
                .NotEmpty();
        }
    }
}
