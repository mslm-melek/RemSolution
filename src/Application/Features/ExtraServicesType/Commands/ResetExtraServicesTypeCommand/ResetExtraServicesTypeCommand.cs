using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.ExtraServicesType.Commands.ResetExtraServicesTypeCommand
{
    // Undoes an agency's renaming of a standard add-on; its price stays
    // (ExtraServicesType.ResetTo).
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record ResetExtraServicesTypeCommand(int Id) : IRequest;

    public class ResetExtraServicesTypeCommandHandler : IRequestHandler<ResetExtraServicesTypeCommand>
    {
        private readonly IApplicationDbContext _context;

        public ResetExtraServicesTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(ResetExtraServicesTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServicesTypes
                .Include(t => t.Template)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.Template is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This type is the agency's own: there is no standard version to go back to.")
                });
            }

            entity.ResetTo(entity.Template);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
