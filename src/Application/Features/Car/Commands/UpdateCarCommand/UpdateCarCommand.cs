using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.Car.Commands.UpdateCarCommand
{
    public record UpdateCarCommand : IRequest
    {
        public int Id { get; init; }
        public int? ModelId { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        public string? ImageUrl { get; init; }
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
    }

    public class UpdateCarCommandHandler : IRequestHandler<UpdateCarCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateCarCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Cars
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.ModelId = request.ModelId;
            entity.FirstCirculationDate = request.FirstCirculationDate;
            entity.Color = request.Color;
            entity.ImageUrl = request.ImageUrl;
            entity.Power = request.Power;
            entity.FuelType = request.FuelType;

            entity.AddDomainEvent(new CarCompletedEvent(entity));

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

}
