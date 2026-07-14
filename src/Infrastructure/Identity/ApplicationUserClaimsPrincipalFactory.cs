using System.Security.Claims;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// Adds the AgencyId claim to the principal so ITenantProvider can resolve the
/// tenant per-request without a database round-trip. Platform admins have no
/// agency and get no claim.
/// Also adds one Permission claim per UserPermission row, so per-permission
/// policies evaluate against the principal without a lookup. The claims are
/// minted at sign-in: granting or revoking a permission takes effect on the
/// next sign-in (refresh the user's security stamp to force it, as
/// AssignAgencyAsync does for the tenant claim).
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly ApplicationDbContext _context;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        ApplicationDbContext context)
        : base(userManager, roleManager, options)
    {
        _context = context;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Platform administrators never carry the tenant claim, even if
        // AgencyId was set on the user by mistake — being tenant-scoped and
        // platform-wide at once must be impossible.
        var isPlatformAdministrator = identity.HasClaim(identity.RoleClaimType, Roles.PlatformAdministrator);

        if (user.AgencyId is int agencyId && !isPlatformAdministrator)
        {
            identity.AddClaim(new Claim(Claims.AgencyId, agencyId.ToString()));
        }

        // Agency administrators hold every permission implicitly — the
        // policies accept the role itself, so materializing claims for
        // them would only bloat the cookie and go stale as permissions
        // are added. Platform administrators operate platform features,
        // never tenant modules, so they carry no permission claims either.
        var isAgencyAdministrator = identity.HasClaim(identity.RoleClaimType, Roles.AgencyAdministrator);

        if (!isPlatformAdministrator && !isAgencyAdministrator)
        {
            var permissions = await _context.UserPermissions
                .AsNoTracking()
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Permission)
                .ToListAsync();

            foreach (var permission in permissions)
            {
                identity.AddClaim(new Claim(Claims.Permission, permission));
            }
        }

        return identity;
    }
}
