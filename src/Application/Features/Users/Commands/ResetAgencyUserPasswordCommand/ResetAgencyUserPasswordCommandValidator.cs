namespace RemSolution.Application.Features.Users.Commands.ResetAgencyUserPasswordCommand
{
    public class ResetAgencyUserPasswordCommandValidator : AbstractValidator<ResetAgencyUserPasswordCommand>
    {
        public ResetAgencyUserPasswordCommandValidator()
        {
            RuleFor(v => v.UserId)
                .NotEmpty();

            // Complexity rules stay with Identity (the one source of truth);
            // this only rejects the obviously empty case before hitting it.
            RuleFor(v => v.NewPassword)
                .NotEmpty();
        }
    }
}
