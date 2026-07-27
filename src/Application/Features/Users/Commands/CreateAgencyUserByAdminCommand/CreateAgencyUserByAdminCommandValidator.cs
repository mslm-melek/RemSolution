using RemSolution.Domain.Constants;

using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Users.Commands.CreateAgencyUserByAdminCommand
{
    public class CreateAgencyUserByAdminCommandValidator : AbstractValidator<CreateAgencyUserByAdminCommand>
    {
        public CreateAgencyUserByAdminCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.AgencyId)
                .GreaterThan(0);

            RuleFor(v => v.UserName)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);

            // Complexity rules stay with Identity (the one source of truth);
            // this only rejects the obviously empty case before hitting it.
            RuleFor(v => v.Password)
                .NotEmpty();

            RuleFor(v => v.Role)
                .Must(r => r == Roles.AgencyAdministrator || r == Roles.AgencyStaff)
                .WithMessage(_ => localizer["Validation.Role.AgencyRoles"]);

            RuleForEach(v => v.Permissions)
                .Must(p => Permissions.All.Contains(p))
                .WithMessage(_ => localizer["Validation.Permission.Unknown"]);
        }
    }
}
