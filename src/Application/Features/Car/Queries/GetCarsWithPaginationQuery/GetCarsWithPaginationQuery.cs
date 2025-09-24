using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery
{
    public record GetCarsWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? ModelId = null,
        string? Color = null,
        FuelType? FuelType = null
    ) : IRequest<PaginatedList<CarDto>>;
    public class GetCarsWithPaginationQueryHandler
        : IRequestHandler<GetCarsWithPaginationQuery, PaginatedList<CarDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public GetCarsWithPaginationQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PaginatedList<CarDto>> Handle(GetCarsWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Cars.AsNoTracking().AsQueryable();

            if (request.ModelId.HasValue)
                query = query.Where(c => c.ModelId == request.ModelId);

            if (!string.IsNullOrWhiteSpace(request.Color))
                query = query.Where(c => c.Color == request.Color);

            if (request.FuelType.HasValue)
                query = query.Where(c => c.FuelType == request.FuelType);

            return await query
                .ProjectTo<CarDto>(_mapper.ConfigurationProvider)
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
