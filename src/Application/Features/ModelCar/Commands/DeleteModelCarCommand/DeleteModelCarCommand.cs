using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.ModelCar.Commands.DeleteModelCarCommand
{
   
    public record DeleteModelCarCommand(int Id) : IRequest;

    public class DeleteModelCarCommandHandler : IRequestHandler<DeleteModelCarCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteModelCarCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteModelCarCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ModelCars
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.ModelCars.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }

    }
}
