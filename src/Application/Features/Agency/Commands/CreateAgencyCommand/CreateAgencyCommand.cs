using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record CreateAgencyCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Address { get; init; }
        public int CountryId { get; init; }
    }

    public class CreateAgencyCommandHandler : IRequestHandler<CreateAgencyCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateAgencyCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateAgencyCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Agency
            {
                Name = request.Name,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Address = request.Address,
                CountryId = request.CountryId,
            };

            _context.Agencies.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
