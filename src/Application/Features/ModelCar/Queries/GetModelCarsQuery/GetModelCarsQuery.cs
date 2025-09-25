using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ModelCar.DTOs;

namespace RemSolution.Application.Features.ModelCar.Queries.GetModelCarsQuery
{
    public record GetModelCarsQuery : IRequest<IList<ModelCarDto>>;

    public class GetModelCarsQueryHandler : IRequestHandler<GetModelCarsQuery, IList<ModelCarDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public GetModelCarsQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IList<ModelCarDto>> Handle(GetModelCarsQuery request, CancellationToken cancellationToken)
        {
            return await _context.ModelCars
                .AsNoTracking()
                .ProjectTo<ModelCarDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
        }
    }
}
