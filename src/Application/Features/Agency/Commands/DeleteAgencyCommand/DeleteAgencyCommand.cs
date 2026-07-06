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
            // IgnoreQueryFilters: this is a platform-admin command and the caller has
            // no tenant, so the tenant filters would hide the very rows being checked.
            // Only booleans escape here, never tenant data.
            var hasTenantData =
                await _context.Cars.IgnoreQueryFilters().AnyAsync(c => c.AgencyId == request.Id, cancellationToken) ||
                await _context.Clients.IgnoreQueryFilters().AnyAsync(c => c.AgencyId == request.Id, cancellationToken) ||
                await _context.Rentings.IgnoreQueryFilters().AnyAsync(r => r.AgencyId == request.Id, cancellationToken) ||
                await _context.Reservations.IgnoreQueryFilters().AnyAsync(r => r.AgencyId == request.Id, cancellationToken) ||
                await _context.Payments.IgnoreQueryFilters().AnyAsync(p => p.AgencyId == request.Id, cancellationToken) ||
                await _context.Expenses.IgnoreQueryFilters().AnyAsync(e => e.AgencyId == request.Id, cancellationToken) ||
                await _context.ExtraServices.IgnoreQueryFilters().AnyAsync(e => e.AgencyId == request.Id, cancellationToken);

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
