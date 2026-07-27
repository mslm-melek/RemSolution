
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Brand.Commands.CreateBrandCommand
{
    public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
    {
        private readonly IApplicationDbContext _context;

        public CreateBrandCommandValidator(IApplicationDbContext context, ILocalizer localizer)
        {
            _context = context;
            RuleFor(v => v.Name)
                .MaximumLength(200).WithMessage(_ => localizer["Validation.Brand.NameMaxLength"])
                .NotEmpty().WithMessage(_ => localizer["Validation.Brand.NameRequired"])
                .MustAsync(BeUniqueName).WithMessage(_ => localizer["Validation.Brand.NameUnique"]);
        }

        private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
        {
            return !await _context.Brands
                .AnyAsync(b => b.Name.ToLower() == name.ToLower(), cancellationToken);
        }
    }
}
