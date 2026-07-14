
namespace RemSolution.Application.Features.Branch.Commands.CreateBranchCommand
{
    public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
    {
        public CreateBranchCommandValidator()
        {
            RuleFor(v => v.Name)
                .MaximumLength(200)
                .NotEmpty();

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
