using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

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

            // Tenant FKs are Restrict: deleting an agency that still owns data would
            // otherwise surface as a raw DbUpdateException (500).
            var hasTenantData =
                await _context.Cars.AnyAsync(c => c.AgencyId == request.Id, cancellationToken) ||
                await _context.Clients.AnyAsync(c => c.AgencyId == request.Id, cancellationToken) ||
                await _context.Rentings.AnyAsync(r => r.AgencyId == request.Id, cancellationToken) ||
                await _context.Reservations.AnyAsync(r => r.AgencyId == request.Id, cancellationToken) ||
                await _context.Payments.AnyAsync(p => p.AgencyId == request.Id, cancellationToken) ||
                await _context.Expenses.AnyAsync(e => e.AgencyId == request.Id, cancellationToken) ||
                await _context.ExtraServices.AnyAsync(e => e.AgencyId == request.Id, cancellationToken);

            if (hasTenantData)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "Agency still has associated data (cars, clients, rentings, ...) and cannot be deleted.")
                });
            }

            _context.Agencies.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
