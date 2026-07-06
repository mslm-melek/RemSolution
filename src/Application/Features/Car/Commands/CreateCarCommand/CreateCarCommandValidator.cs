using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Car.Commands.CreateCarCommand
{
    public class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
    {
        private readonly IApplicationDbContext _context;

        public CreateCarCommandValidator(IApplicationDbContext context)
        {
            _context = context;

            RuleFor(v => v.AgencyId)
                .GreaterThan(0)
                .MustAsync(AgencyExists).WithMessage("Agency does not exist.");
            RuleFor(v => v.Matricule)
                .MaximumLength(200)
                .NotEmpty();
            RuleFor(x => x.ModelId)
                .NotNull().WithMessage("ModelId is required.");
        }

        private async Task<bool> AgencyExists(int agencyId, CancellationToken cancellationToken)
        {
            return await _context.Agencies
                .AnyAsync(a => a.Id == agencyId, cancellationToken);
        }
    }
}
