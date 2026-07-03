using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Brand.Commands.UpdateBrandCommand
{
    public record UpdateBrandCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateBrandCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Brands
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
