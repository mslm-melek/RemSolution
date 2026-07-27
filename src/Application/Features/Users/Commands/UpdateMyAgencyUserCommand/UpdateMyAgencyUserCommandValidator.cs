using RemSolution.Domain.Constants;

using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyAgencyUserCommand
{
    public class UpdateMyAgencyUserCommandValidator : AbstractValidator<UpdateMyAgencyUserCommand>
    {
        public UpdateMyAgencyUserCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.UserId)
                .NotEmpty();

            RuleForEach(v => v.Permissions)
                .Must(p => Permissions.All.Contains(p))
                .WithMessage(_ => localizer["Validation.Permission.Unknown"]);
        }
    }
}
