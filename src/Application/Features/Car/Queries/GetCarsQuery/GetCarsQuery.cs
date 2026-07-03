using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Car.DTOs;

namespace RemSolution.Application.Features.Car.Queries.GetCarsQuery
{
    public record GetCarsQuery : IRequest<IList<CarDto>>;

    public class GetCarsQueryHandler : IRequestHandler<GetCarsQuery, IList<CarDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetCarsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<CarDto>> Handle(GetCarsQuery request, CancellationToken cancellationToken)
        {
            return await _context.Cars
                .AsNoTracking()
                .ProjectToType<CarDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
