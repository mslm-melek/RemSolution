using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingsWithPaginationQuery
{
    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? CarId = null,
        int? ClientId = null,
        RentingState? State = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null
    ) : IRequest<PaginatedList<RentingDto>>;

    public class GetRentingsWithPaginationQueryHandler
        : IRequestHandler<GetRentingsWithPaginationQuery, PaginatedList<RentingDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetRentingsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<RentingDto>> Handle(
            GetRentingsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Rentings.AsNoTracking().AsQueryable();

            if (request.CarId.HasValue)
                query = query.Where(r => r.CarId == request.CarId);

            if (request.ClientId.HasValue)
                query = query.Where(r => r.ClientId == request.ClientId || r.SecondClientId == request.ClientId);

            if (request.State.HasValue)
                query = query.Where(r => r.RentingState == request.State);

            if (request.FromDate.HasValue)
                query = query.Where(r => r.EndDate >= request.FromDate);

            if (request.ToDate.HasValue)
                query = query.Where(r => r.StartDate <= request.ToDate);

            return await query
                .OrderByDescending(r => r.StartDate)
                .ProjectToType<RentingDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
