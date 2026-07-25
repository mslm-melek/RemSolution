using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.ExtraServicesType.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesType.Queries.GetExtraServicesTypesQuery
{
    // Readable by any authenticated user whose agency has the ExtraServices
    // feature (staff select types when adding an extra service); management is
    // admin-only above. The platform admin has no tenant, so the gate passes.
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
