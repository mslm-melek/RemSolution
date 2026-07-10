using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Brand.Commands.DeleteBrandCommand
{

    [Auditable("DeleteBrand", "Brand")]
    public record DeleteBrandCommand(int Id) : IRequest;

    public class DeleteBrandCommandHandler : IRequestHandler<DeleteBrandCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteBrandCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Brands
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Brands.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }

    }
}
