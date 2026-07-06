using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Commands.CreateCarCommand
{
    public record CreateCarCommand : IRequest<int>
    {
        public int AgencyId { get; init; }
        public string Matricule { get; init; } = string.Empty;
        public int? ModelId { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        public string? ImageUrl { get; init; }
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
    }
    public class CreateCarCommandHandler : IRequestHandler<CreateCarCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateCarCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Car
            {
                AgencyId = request.AgencyId,
                Matricule = request.Matricule,
                ModelId = request.ModelId,
                FirstCirculationDate= request.FirstCirculationDate,
                Color = request.Color,
                Power = request.Power,
                FuelType = request.FuelType
            };

            _context.Cars.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
