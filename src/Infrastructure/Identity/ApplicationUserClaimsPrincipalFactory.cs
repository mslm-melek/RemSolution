using System.Security.Claims;
using RemSolution.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// Adds the AgencyId claim to the principal so ITenantProvider can resolve the
/// tenant per-request without a database round-trip. Platform admins have no
/// agency and get no claim.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.AgencyId is int agencyId)
        {
            identity.AddClaim(new Claim(Claims.AgencyId, agencyId.ToString()));
        }

        return identity;
    }
}
