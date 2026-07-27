using RemSolution.Domain.Constants;

using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Users.Commands.CreateAgencyUserCommand
{
    public class CreateAgencyUserCommandValidator : AbstractValidator<CreateAgencyUserCommand>
    {
        public CreateAgencyUserCommandValidator(ILocalizer localizer)
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
                .WithMessage(_ => localizer["Validation.Permission.Unknown"]);
        }
    }
}
