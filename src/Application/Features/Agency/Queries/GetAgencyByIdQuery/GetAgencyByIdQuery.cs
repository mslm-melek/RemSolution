using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Agency.DTOs;

namespace RemSolution.Application.Features.Agency.Queries.GetAgencyByIdQuery
{
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
