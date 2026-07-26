using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Payment.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Payment.Queries.GetPaymentsWithPaginationQuery
{
    [Authorize(Policy = Permissions.PaymentRead)]
    [RequiresFeature(FeatureFlags.Payments)]
    public record GetPaymentsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? RentingId = null,
        int? ClientId = null,
        int? ReservationId = null
    ) : IRequest<PaginatedList<PaymentDto>>;

    public class GetPaymentsWithPaginationQueryHandler
        : IRequestHandler<GetPaymentsWithPaginationQuery, PaginatedList<PaymentDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetPaymentsWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<PaymentDto>> Handle(
            GetPaymentsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Payments.AsNoTracking().AsQueryable();

            if (request.RentingId.HasValue)
                query = query.Where(p => p.RentingId == request.RentingId);

            if (request.ClientId.HasValue)
                query = query.Where(p => p.ClientId == request.ClientId);

            if (request.ReservationId.HasValue)
                query = query.Where(p => p.ReservationId == request.ReservationId);

            return await query
                .OrderByDescending(p => p.PayementDate)
                .ProjectToType<PaymentDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
