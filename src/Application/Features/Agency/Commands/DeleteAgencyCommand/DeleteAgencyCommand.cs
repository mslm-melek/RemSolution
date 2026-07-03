using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand
{

    public record DeleteAgencyCommand(int Id) : IRequest;

    public class DeleteAgencyCommandHandler : IRequestHandler<DeleteAgencyCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteAgencyCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteAgencyCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Agencies
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Agencies.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
