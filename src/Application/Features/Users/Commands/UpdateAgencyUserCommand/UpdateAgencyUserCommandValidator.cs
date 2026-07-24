using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Users.Commands.UpdateAgencyUserCommand
{
    public class UpdateAgencyUserCommandValidator : AbstractValidator<UpdateAgencyUserCommand>
    {
        public UpdateAgencyUserCommandValidator()
        {
            RuleFor(v => v.UserId)
                .NotEmpty();

            RuleFor(v => v.Role)
                .Must(r => r == Roles.AgencyAdministrator || r == Roles.AgencyStaff)
                .When(v => v.Role is not null)
                .WithMessage("Role must be AgencyAdministrator or AgencyStaff.");

            RuleForEach(v => v.Permissions)
                .Must(p => Permissions.All.Contains(p))
                .WithMessage("'{PropertyValue}' is not a known permission.");
        }
    }
}
