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

    // The home-screen shortcut tiles this user pinned, in the order they chose,
    // as a comma-separated list of Domain.Constants.HomeWidgets keys. Null means
    // "never chosen" — the SPA then shows its default set; an empty string is a
    // deliberate "no tiles", which is why the two are not the same value.
    public string? HomeWidgets { get; set; }

    // The quick actions this user keeps on their landing screen, in the order they
    // chose, as a comma-separated list of Domain.Constants.HomeActions keys. Null
    // means "never chosen" — the SPA then shows its default set; an empty string
    // is a deliberate "no actions", which is why the two are not the same value.
    public string? HomeActions { get; set; }

    // Set when the account was created with a password somebody else chose —
    // today only the client-account provisioning flow, which emails a generated
    // temporary password. Until the user replaces it the account can sign in but
    // can do nothing else: the claim rides in the ticket and
    // PasswordChangeRequiredMiddleware refuses every other request. Cleared by
    // ChangeMyPasswordCommand and by the Razor change-password page.
    public bool MustChangePassword { get; set; }
}
