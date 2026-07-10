using FluentValidation.Results;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand
{

    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("DeleteAgency", "Agency")]
    public record DeleteAgencyCommand(int Id) : IRequest;

    public class DeleteAgencyCommandHandler : IRequestHandler<DeleteAgencyCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICrossTenantAccess _crossTenant;

        public DeleteAgencyCommandHandler(IApplicationDbContext context, ICrossTenantAccess crossTenant)
        {
            _context = context;
            _crossTenant = crossTenant;
        }

        public async Task Handle(DeleteAgencyCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Agencies
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Tenant FKs are Restrict: deleting an agency that still owns data would
            // otherwise surface as a raw DbUpdateException (500).
            // The caller has no tenant, so tenant filters would hide the very rows
            // being checked — the audited cross-tenant path is the sanctioned bypass.
            // Only booleans escape here, never tenant data.
            var scope = await _crossTenant.BeginAuditedAccessAsync(
                $"Referential-integrity check before deleting agency {request.Id}", cancellationToken);

            var hasTenantData =
                await scope.Query<Domain.Entities.Car>().AnyAsync(c => c.AgencyId == request.Id, cancellationToken) ||
                await scope.Query<Domain.Entities.Client>().AnyAsync(c => c.AgencyId == request.Id, cancellationToken) ||
                await scope.Query<Domain.Entities.Renting>().AnyAsync(r => r.AgencyId == request.Id, cancellationToken) ||
                await scope.Query<Domain.Entities.Reservation>().AnyAsync(r => r.AgencyId == request.Id, cancellationToken) ||
                await scope.Query<Domain.Entities.Payment>().AnyAsync(p => p.AgencyId == request.Id, cancellationToken) ||
                await scope.Query<Domain.Entities.Expense>().AnyAsync(e => e.AgencyId == request.Id, cancellationToken) ||
                await scope.Query<Domain.Entities.ExtraService>().AnyAsync(e => e.AgencyId == request.Id, cancellationToken);

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
