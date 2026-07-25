using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.ExpenseType.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExpenseType.Queries.GetExpenseTypesQuery
{
    // Readable by any authenticated user whose agency has the Expenses feature;
    // management is admin-only above. The platform admin has no tenant, so the
    // gate passes.
    [RequiresFeature(FeatureFlags.Expenses)]
    public record GetExpenseTypesQuery(bool OnlyActive = false) : IRequest<IList<ExpenseTypeDto>>;

    public class GetExpenseTypesQueryHandler : IRequestHandler<GetExpenseTypesQuery, IList<ExpenseTypeDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExpenseTypesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ExpenseTypeDto>> Handle(
            GetExpenseTypesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.ExpenseTypes.AsNoTracking().AsQueryable();

            if (request.OnlyActive)
                query = query.Where(t => t.IsActive);

            return await query
                .OrderBy(t => t.Name)
                .ProjectToType<ExpenseTypeDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
