using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Client.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery
{
    [Authorize(Policy = Permissions.ClientRead)]
    [RequiresFeature(FeatureFlags.Clients)]
    public record GetClientsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        string? Search = null,
        string? CIN = null,
        bool? Flagged = null,
        // Half-open [AddedFrom, AddedTo) over when the client was recorded, which
        // is the only "added on" the model has.
        DateTimeOffset? AddedFrom = null,
        DateTimeOffset? AddedTo = null,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the name.
        string? SortBy = null,
        bool SortDescending = false
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

            if (request.Flagged.HasValue)
                query = query.Where(c => c.IsFlagged == request.Flagged);

            if (request.AddedFrom.HasValue)
                query = query.Where(c => c.CreatedOn >= request.AddedFrom);

            if (request.AddedTo.HasValue)
                query = query.Where(c => c.CreatedOn < request.AddedTo);

            var descending = request.SortDescending;

            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "birthdate" => query.OrderByField(c => c.BirthDate, descending),
                "cin" => query.OrderByField(c => c.CIN, descending),
                "flagged" => query.OrderByField(c => c.IsFlagged, descending),
                // Same figure the row shows: both seats, cancelled ones excluded
                // (see ClientDto.RentingCount).
                "rentings" => query.OrderByField(
                    c => c.Rentings!.Count(r => r.RentingState != RentingState.Cancelled)
                         + c.SecondRentings!.Count(r => r.RentingState != RentingState.Cancelled),
                    descending),
                _ => query.OrderByField(c => c.LastName, descending)
                          .ThenByField(c => c.FirstName, descending),
            };

            return await ordered
                .ThenBy(c => c.Id)
                .ProjectToType<ClientDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
