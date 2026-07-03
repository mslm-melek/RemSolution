using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Car.DTOs;

namespace RemSolution.Application.Features.Car.Queries.GetCarByIdQuery
{
    public record GetCarByIdQuery(int Id) : IRequest<CarDto?>;

    public class GetCarByIdQueryHandler : IRequestHandler<GetCarByIdQuery, CarDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetCarByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CarDto?> Handle(GetCarByIdQuery request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
              .Where(c => c.Id == request.Id)
              .ProjectToType<CarDto>()
              .FirstOrDefaultAsync(cancellationToken);

            if (car == null)
                throw new NotFoundException(nameof(Car), request.Id.ToString());

            return car;
        }

    }
}
