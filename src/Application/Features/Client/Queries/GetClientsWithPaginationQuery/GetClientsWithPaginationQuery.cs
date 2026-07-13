using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Client.DTOs;

namespace RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery
{
    public record GetClientsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        string? Search = null,
        string? CIN = null
    ) : IRequest<PaginatedList<ClientDto>>;

    public class GetClientsWithPaginationQueryHandler
        : IRequestHandler<GetClientsWithPaginationQuery, PaginatedList<ClientDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetClientsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<ClientDto>> Handle(GetClientsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Clients.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
                query = query.Where(c =>
                    (c.FirstName != null && c.FirstName.Contains(request.Search)) ||
                    (c.LastName != null && c.LastName.Contains(request.Search)));

            if (!string.IsNullOrWhiteSpace(request.CIN))
                query = query.Where(c => c.CIN == request.CIN);

            return await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ProjectToType<ClientDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
