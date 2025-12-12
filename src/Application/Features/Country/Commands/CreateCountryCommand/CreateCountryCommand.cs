using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Events;

namespace RemSolution.Application.Features.Country.Commands.CreateCountryCommand
{
    public record CreateCountryCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
    }
    public class CreateCountryCommandHandler : IRequestHandler<CreateCountryCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateCountryCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateCountryCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Country
            {
                Name = request.Name,
            };

            _context.Countries.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
