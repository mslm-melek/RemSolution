using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Agency.Models;
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
        // The HQ pin for the address above, as picked on the map. Set as a pair
        // or not at all (see the validator).
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public int CountryId { get; init; }
        // Settings persisted to the agency's AgencySettings row (see P.9).
        // ISO 4217 code the agency trades in; every Money amount it stores uses it.
        public string Currency { get; init; } = "TND";
        public int CancellationWindowHours { get; init; } = 24;
        public int ReservationExpiryHours { get; init; } = 48;
        // The agency's locations, created with it: branches are where customers
        // actually collect cars, so setting one up is part of creating the agency
        // rather than a follow-up step that is easy to forget. Editing them later
        // goes through the Agencies/{id}/branches sub-resource.
        public IReadOnlyList<AgencyBranchInput> Branches { get; init; } = Array.Empty<AgencyBranchInput>();
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
                Location = GeoPoint.ToPoint(request.Latitude, request.Longitude),
                CountryId = request.CountryId,
                // The 1:1 settings row is inserted with the agency (EF wires the FK).
                Settings = new RemSolution.Domain.Entities.AgencySettings
                {
                    CurrencyCode = request.Currency.Trim().ToUpperInvariant(),
                    CancellationWindowHours = request.CancellationWindowHours,
                    ReservationExpiryHours = request.ReservationExpiryHours,
                },
            };

            // Agency and branches land together or not at all: disposing without
            // commit rolls back, so a failure part-way through cannot leave a live
            // agency holding only some of the locations entered for it.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            _context.Agencies.Add(entity);

            // Saved first so the branches below have an agency id to belong to.
            await _context.SaveChangesAsync(cancellationToken);

            if (request.Branches.Count > 0)
            {
                // Branch is an ITenantEntity and the platform administrator
                // creating this agency has no tenant of their own, so the write
                // interceptor would leave AgencyId unstamped. Acting as the new
                // agency is what makes the write behave exactly as it does for one
                // of the agency's own users.
                //
                // Administrative, because the agency has no subscription at this
                // point — one is assigned once it exists — and subscription
                // enforcement would otherwise refuse these inserts.
                using (AmbientTenant.PushAdministrative(entity.Id))
                {
                    foreach (var branch in request.Branches)
                    {
                        _context.Branches.Add(new RemSolution.Domain.Entities.Branch
                        {
                            Name = branch.Name,
                            CountryId = branch.CountryId,
                            Address = branch.Address,
                            Location = GeoPoint.ToPoint(branch.Latitude, branch.Longitude),
                        });
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }
}
