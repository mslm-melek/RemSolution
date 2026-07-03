using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Country.Commands.UpdateCountryCommand
{
    public record UpdateCountryCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public class UpdateCountryCommandHandler : IRequestHandler<UpdateCountryCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateCountryCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateCountryCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Countries
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
