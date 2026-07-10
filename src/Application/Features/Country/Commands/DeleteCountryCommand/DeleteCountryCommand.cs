using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.Features.Country.Commands.DeleteCountryCommand
{

    [Auditable("DeleteCountry", "Country")]
    public record DeleteCountryCommand(int Id) : IRequest;

    public class DeleteCountryCommandHandler : IRequestHandler<DeleteCountryCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteCountryCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteCountryCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Countries
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Countries.Remove(entity);


            await _context.SaveChangesAsync(cancellationToken);
        }

    }
}
