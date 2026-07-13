using Microsoft.AspNetCore.Identity;

namespace RemSolution.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // The agency this user belongs to; the source of the AgencyId claim.
    // Null for platform admins, who are not scoped to any tenant.
    public int? AgencyId { get; set; }

    public string? FullName { get; set; }
}
