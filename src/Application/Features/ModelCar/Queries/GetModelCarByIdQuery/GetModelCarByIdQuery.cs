using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ModelCar.DTOs;

namespace RemSolution.Application.Features.ModelCar.Queries.GetModelCarByIdQuery
{
    public record GetModelCarByIdQuery(int Id) : IRequest<ModelCarDto?>;

    public class GetModelCarByIdQueryHandler : IRequestHandler<GetModelCarByIdQuery, ModelCarDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;


        public GetModelCarByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

        }

        public async Task<ModelCarDto?> Handle(GetModelCarByIdQuery request, CancellationToken cancellationToken)
        {
            var modelCar = await _context.ModelCars
              .Where(c => c.Id == request.Id)
              .ProjectTo<ModelCarDto>(_mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            if (modelCar == null)
                throw new NotFoundException(nameof(Car), request.Id.ToString());

            return modelCar;
        }

    }
}
