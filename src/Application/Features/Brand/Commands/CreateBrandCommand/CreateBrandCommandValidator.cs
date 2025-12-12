
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Brand.Commands.CreateBrandCommand
{
    public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
    {
        private readonly IApplicationDbContext _context;

        public CreateBrandCommandValidator(IApplicationDbContext context)
        {
            _context = context;
            RuleFor(v => v.Name)
                .MaximumLength(200).WithMessage("Brand name must not exceed 200 characters.")
                .NotEmpty().WithMessage("Brand name is required.")
                .MustAsync(BeUniqueName).WithMessage("Brand name must be unique.");
        }

        private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
        {
            return !await _context.Brands
                .AnyAsync(b => b.Name.ToLower() == name.ToLower(), cancellationToken);
        }
    }
}
