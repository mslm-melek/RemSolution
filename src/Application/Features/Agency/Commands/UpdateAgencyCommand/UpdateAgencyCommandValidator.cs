
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
        }
    }
}
