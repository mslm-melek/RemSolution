using Microsoft.AspNetCore.Identity;

namespace RemSolution.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // The agency this user belongs to; the source of the AgencyId claim.
    // Null for platform admins, who are not scoped to any tenant.
    public int? AgencyId { get; set; }

    public string? FullName { get; set; }

    // The user's chosen UI language (see Domain.Constants.Languages). Null means
    // "never chosen" — the request then falls back to the culture cookie, the
    // Accept-Language header and finally the default culture.
    public string? PreferredLanguage { get; set; }
}
