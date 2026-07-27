using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Brand.Commands.UpdateBrandCommand
{
    public class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateBrandCommandValidator(IApplicationDbContext context, ILocalizer localizer)
        {
            _context = context;
            RuleFor(v => v.Name)
                .MaximumLength(200).WithMessage(_ => localizer["Validation.Brand.NameMaxLength"])
                .NotEmpty().WithMessage(_ => localizer["Validation.Brand.NameRequired"])
                .MustAsync(BeUniqueName).WithMessage(_ => localizer["Validation.Brand.NameUnique"]);
        }

        private async Task<bool> BeUniqueName(UpdateBrandCommand command, string name, CancellationToken cancellationToken)
        {
            return !await _context.Brands
                .AnyAsync(b => b.Id != command.Id && b.Name.ToLower() == name.ToLower(), cancellationToken);
        }
    }
}
