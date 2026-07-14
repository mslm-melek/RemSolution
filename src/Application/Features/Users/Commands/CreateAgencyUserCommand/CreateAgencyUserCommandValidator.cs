using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Users.Commands.CreateAgencyUserCommand
{
    public class CreateAgencyUserCommandValidator : AbstractValidator<CreateAgencyUserCommand>
    {
        public CreateAgencyUserCommandValidator()
        {
            RuleFor(v => v.UserName)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);

            // Complexity rules stay with Identity (the one source of truth);
            // this only rejects the obviously empty case before hitting it.
            RuleFor(v => v.Password)
                .NotEmpty();

            RuleForEach(v => v.Permissions)
                .Must(p => Permissions.All.Contains(p))
                .WithMessage("'{PropertyValue}' is not a known permission.");
        }
    }
}
