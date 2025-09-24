using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Car.DTOs;

namespace RemSolution.Application.Features.Car.Queries.GetCarByIdQuery
{
    public record GetCarByIdQuery(int Id) : IRequest<CarDto?>;

    public class GetCarByIdQueryHandler : IRequestHandler<GetCarByIdQuery, CarDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;


        public GetCarByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

        }

        public async Task<CarDto?> Handle(GetCarByIdQuery request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
              .Where(c => c.Id == request.Id)
              .ProjectTo<CarDto>(_mapper.ConfigurationProvider)
              .FirstOrDefaultAsync(cancellationToken);

            if (car == null)
                throw new NotFoundException(nameof(Car), request.Id.ToString());

            return car;
        }

    }
}
