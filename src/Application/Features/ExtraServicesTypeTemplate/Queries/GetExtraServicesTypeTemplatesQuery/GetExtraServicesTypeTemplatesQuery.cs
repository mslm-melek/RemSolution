using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesTypeTemplate.Queries.GetExtraServicesTypeTemplatesQuery
{
    // The add-ons the platform offers every agency.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record GetExtraServicesTypeTemplatesQuery : IRequest<IList<ExtraServicesTypeTemplateDto>>;

    public class GetExtraServicesTypeTemplatesQueryHandler
        : IRequestHandler<GetExtraServicesTypeTemplatesQuery, IList<ExtraServicesTypeTemplateDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExtraServicesTypeTemplatesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ExtraServicesTypeTemplateDto>> Handle(
            GetExtraServicesTypeTemplatesQuery request, CancellationToken cancellationToken) =>
            await _context.ExtraServicesTypeTemplates
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ProjectToType<ExtraServicesTypeTemplateDto>()
                .ToListAsync(cancellationToken);
    }
}
