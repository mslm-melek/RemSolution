using FluentValidation.Results;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Application.Features.Agency.Models;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand
{
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record CreateAgencyCommand : IRequest<AgencyCreatedDto>
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

        // Who runs the agency: the administrator login is created with it, so no
        // agency exists that nobody can sign in to. Empty falls back to the
        // agency's own email; the validator refuses the command when neither is set.
        public string? AdminEmail { get; init; }
        public string? AdminFullName { get; init; }

        /// <summary>
        /// The address the administrator login is created for. A method, not a
        /// property, so it stays out of the API schema.
        /// </summary>
        public string? ResolveAdminEmail() =>
            string.IsNullOrWhiteSpace(AdminEmail) ? Trimmed(Email) : AdminEmail.Trim();

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class CreateAgencyCommandHandler : IRequestHandler<CreateAgencyCommand, AgencyCreatedDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencyAccountService _accounts;
        private readonly ILocalizer _localizer;

        public CreateAgencyCommandHandler(
            IApplicationDbContext context, IAgencyAccountService accounts, ILocalizer localizer)
        {
            _context = context;
            _accounts = accounts;
            _localizer = localizer;
        }

        public async Task<AgencyCreatedDto> Handle(CreateAgencyCommand request, CancellationToken cancellationToken)
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

            // Agency, branches and administrator land together or not at all.
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

            // Inside the transaction, on the same DbContext.
            var admin = await _accounts.EnsureAdministratorAsync(
                entity.Id, entity.Name, request.ResolveAdminEmail(), request.AdminFullName, cancellationToken);

            if (admin.Outcome == AgencyAdminOutcome.EmailTaken)
            {
                // A 400 naming the field, not a 500; throwing rolls the agency back.
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.AdminEmail),
                        _localizer["Validation.Agency.AdminEmailTaken", admin.Email ?? string.Empty]),
                });
            }

            await transaction.CommitAsync(cancellationToken);

            // After the commit, and never throws: a mail cannot be unsent.
            var sent = await _accounts.SendWelcomeAsync(admin, cancellationToken);

            return new AgencyCreatedDto
            {
                Id = entity.Id,
                AdminUserName = admin.Email,
                AdminTemporaryPassword = admin.TemporaryPassword,
                WelcomeEmailSent = sent,
            };
        }
    }
}
