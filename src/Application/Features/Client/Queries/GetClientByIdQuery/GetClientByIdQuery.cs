using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Client.DTOs;

namespace RemSolution.Application.Features.Client.Queries.GetClientByIdQuery
{
    public record GetClientByIdQuery(int Id) : IRequest<ClientDto>;

    public class GetClientByIdQueryHandler : IRequestHandler<GetClientByIdQuery, ClientDto>
    {
        private readonly IApplicationDbContext _context;

        public GetClientByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ClientDto> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
        {
            var client = await _context.Clients
              .Where(c => c.Id == request.Id)
              .ProjectToType<ClientDto>()
              .FirstOrDefaultAsync(cancellationToken);

            if (client == null)
                throw new NotFoundException(nameof(Client), request.Id.ToString());

            return client;
        }
    }
}
