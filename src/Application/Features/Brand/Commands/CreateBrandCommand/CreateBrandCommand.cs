using RemSolution.Application.Common.Interfaces;


namespace RemSolution.Application.Features.Brand.Commands.CreateBrandCommand
{
    public record CreateBrandCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
    }
    public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateBrandCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
        {
            var entity = new Domain.Entities.Brand
            {
                Name = request.Name
            };

            _context.Brands.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
