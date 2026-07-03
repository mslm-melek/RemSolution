using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Agency.DTOs;

namespace RemSolution.Application.Features.Agency.Queries.GetAgenciesQuery
{
    public record GetAgenciesQuery : IRequest<IList<AgencyDto>>;

    public class GetAgenciesQueryHandler : IRequestHandler<GetAgenciesQuery, IList<AgencyDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetAgenciesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<AgencyDto>> Handle(GetAgenciesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Agencies
                .AsNoTracking()
                .ProjectToType<AgencyDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
