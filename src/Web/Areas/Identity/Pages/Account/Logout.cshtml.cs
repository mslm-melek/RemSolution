using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    // Signing out on GET lets the SPA use a plain link. Logout CSRF is an
    // accepted trade-off here: the worst an attacker can do is sign the user
    // out.
    public async Task<IActionResult> OnGetAsync()
    {
        if (_signInManager.IsSignedIn(User))
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");
        }

        return RedirectToPage("./Login");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");

        if (returnUrl is not null)
            return LocalRedirect(returnUrl);

        return RedirectToPage("./Login");
    }
}
