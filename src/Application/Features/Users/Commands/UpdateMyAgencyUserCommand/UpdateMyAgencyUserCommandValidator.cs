using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Users.Commands.UpdateMyAgencyUserCommand
{
    public class UpdateMyAgencyUserCommandValidator : AbstractValidator<UpdateMyAgencyUserCommand>
    {
        public UpdateMyAgencyUserCommandValidator()
        {
            RuleFor(v => v.UserId)
                .NotEmpty();

            RuleForEach(v => v.Permissions)
                .Must(p => Permissions.All.Contains(p))
                .WithMessage("'{PropertyValue}' is not a known permission.");
        }
    }
}
