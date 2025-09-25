using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand
{
    public record CreateModelCarCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public int? BrandId { get; init; }
    }
    public class CreateModelCarCommandHandler : IRequestHandler<CreateModelCarCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateModelCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateModelCarCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.ModelCar
            {
                Name = request.Name,
                BrandId = request.BrandId
            };

            entity.AddDomainEvent(new ModelCarCompletedEvent(entity));

            _context.ModelCars.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
