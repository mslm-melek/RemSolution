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

    // RETIRED 2026-10-06 with the home-screen shortcuts and quick actions: nothing
    // reads or writes these any more. They stay mapped for one release because
    // the migration runs before the new code ships, and the version still serving
    // during that window reads both (docs/RUNBOOK_Base_De_Donnees.md §1). Drop
    // them in the next release's migration.
    public string? HomeWidgets { get; set; }

    public string? HomeActions { get; set; }

    // Set when the account was created with a password somebody else chose —
    // today only the client-account provisioning flow, which emails a generated
    // temporary password. Until the user replaces it the account can sign in but
    // can do nothing else: the claim rides in the ticket and
    // PasswordChangeRequiredMiddleware refuses every other request. Cleared by
    // ChangeMyPasswordCommand and by the Razor change-password page.
    public bool MustChangePassword { get; set; }
}
