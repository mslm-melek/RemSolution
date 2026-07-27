using RemSolution.Domain.Constants;

using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Users.Commands.UpdateAgencyUserCommand
{
    public class UpdateAgencyUserCommandValidator : AbstractValidator<UpdateAgencyUserCommand>
    {
        public UpdateAgencyUserCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.UserId)
                .NotEmpty();

            RuleFor(v => v.Role)
                .Must(r => r == Roles.AgencyAdministrator || r == Roles.AgencyStaff)
                .When(v => v.Role is not null)
                .WithMessage(_ => localizer["Validation.Role.AgencyRoles"]);

            RuleForEach(v => v.Permissions)
                .Must(p => Permissions.All.Contains(p))
                .WithMessage(_ => localizer["Validation.Permission.Unknown"]);
        }
    }
}
