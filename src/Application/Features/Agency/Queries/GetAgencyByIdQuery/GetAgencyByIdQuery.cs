using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Queries.GetAgencyByIdQuery
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record GetAgencyByIdQuery(int Id) : IRequest<AgencyDto?>;

    public class GetAgencyByIdQueryHandler : IRequestHandler<GetAgencyByIdQuery, AgencyDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencyByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AgencyDto?> Handle(GetAgencyByIdQuery request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
              .Where(a => a.Id == request.Id)
              .ProjectToType<AgencyDto>()
              .FirstOrDefaultAsync(cancellationToken);

            if (agency == null)
                throw new NotFoundException(nameof(Domain.Entities.Agency), request.Id.ToString());

            return agency;
        }
    }
}
