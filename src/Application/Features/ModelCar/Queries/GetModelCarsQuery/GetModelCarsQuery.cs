using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ModelCar.DTOs;

namespace RemSolution.Application.Features.ModelCar.Queries.GetModelCarsQuery
{
    public record GetModelCarsQuery : IRequest<IList<ModelCarDto>>;

    public class GetModelCarsQueryHandler : IRequestHandler<GetModelCarsQuery, IList<ModelCarDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetModelCarsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ModelCarDto>> Handle(GetModelCarsQuery request, CancellationToken cancellationToken)
        {
            return await _context.ModelCars
                .AsNoTracking()
                .ProjectToType<ModelCarDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
