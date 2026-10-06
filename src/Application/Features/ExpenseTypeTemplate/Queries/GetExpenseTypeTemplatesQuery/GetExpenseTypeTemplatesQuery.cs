using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.ExpenseTypeTemplate.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExpenseTypeTemplate.Queries.GetExpenseTypeTemplatesQuery
{
    // The expense types the platform offers every agency.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record GetExpenseTypeTemplatesQuery : IRequest<IList<ExpenseTypeTemplateDto>>;

    public class GetExpenseTypeTemplatesQueryHandler
        : IRequestHandler<GetExpenseTypeTemplatesQuery, IList<ExpenseTypeTemplateDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExpenseTypeTemplatesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ExpenseTypeTemplateDto>> Handle(
            GetExpenseTypeTemplatesQuery request, CancellationToken cancellationToken) =>
            await _context.ExpenseTypeTemplates
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ProjectToType<ExpenseTypeTemplateDto>()
                .ToListAsync(cancellationToken);
    }
}
