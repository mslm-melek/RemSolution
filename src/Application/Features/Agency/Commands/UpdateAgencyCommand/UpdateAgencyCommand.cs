using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.UpdateAgencyCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record UpdateAgencyCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Address { get; init; }
        public int CountryId { get; init; }
    }

    public class UpdateAgencyCommandHandler : IRequestHandler<UpdateAgencyCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateAgencyCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateAgencyCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Agencies
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.Email = request.Email;
            entity.PhoneNumber = request.PhoneNumber;
            entity.Address = request.Address;
            entity.CountryId = request.CountryId;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
