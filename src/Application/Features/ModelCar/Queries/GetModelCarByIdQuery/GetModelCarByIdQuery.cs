using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ModelCar.DTOs;

namespace RemSolution.Application.Features.ModelCar.Queries.GetModelCarByIdQuery
{
    public record GetModelCarByIdQuery(int Id) : IRequest<ModelCarDto?>;

    public class GetModelCarByIdQueryHandler : IRequestHandler<GetModelCarByIdQuery, ModelCarDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetModelCarByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ModelCarDto?> Handle(GetModelCarByIdQuery request, CancellationToken cancellationToken)
        {
            var modelCar = await _context.ModelCars
              .Where(c => c.Id == request.Id)
              .ProjectToType<ModelCarDto>()
              .FirstOrDefaultAsync(cancellationToken);

            if (modelCar == null)
                throw new NotFoundException(nameof(Domain.Entities.ModelCar), request.Id.ToString());

            return modelCar;
        }

    }
}
