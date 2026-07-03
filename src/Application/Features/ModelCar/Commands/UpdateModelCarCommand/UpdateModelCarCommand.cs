using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.ModelCar.Commands.UpdateModelCarCommand
{
    public record UpdateModelCarCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? BrandId { get; init; }
    }

    public class UpdateModelCarCommandHandler : IRequestHandler<UpdateModelCarCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateModelCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateModelCarCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ModelCars
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.BrandId = request.BrandId;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
