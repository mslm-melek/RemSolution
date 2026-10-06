using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.ExtraServicesType.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesType.Queries.GetExtraServicesTypesQuery
{
    // The agency's own catalog (its copies of the platform's templates plus what
    // it added), readable by any of its users with the ExtraServices feature —
    // staff pick a type when adding an extra service. Management is admin-only.
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record GetExtraServicesTypesQuery(bool OnlyActive = false) : IRequest<IList<ExtraServicesTypeDto>>;

    public class GetExtraServicesTypesQueryHandler
        : IRequestHandler<GetExtraServicesTypesQuery, IList<ExtraServicesTypeDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExtraServicesTypesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ExtraServicesTypeDto>> Handle(
            GetExtraServicesTypesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ExtraServicesTypes.AsNoTracking().AsQueryable();

            if (request.OnlyActive)
                query = query.Where(t => t.IsActive);

            return await query
                .OrderBy(t => t.Name)
                .ProjectToType<ExtraServicesTypeDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
