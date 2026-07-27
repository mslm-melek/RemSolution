using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Identity;
using RemSolution.Web.Infrastructure;

namespace RemSolution.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _localizer = localizer;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Identity.Validation.Required")]
        [EmailAddress(ErrorMessage = "Identity.Validation.Email")]
        [Display(Name = "Identity.Field.Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identity.Validation.Required")]
        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Identity.Field.RememberMe")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        // Clear the existing external cookie to ensure a clean login process.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in", Input.Email);

            // Seed the culture cookie from the account's stored language so the
            // SPA — which resolves its language from that cookie before it
            // bootstraps — opens in the right one on a device that has never
            // seen this user.
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (Languages.Normalize(user?.PreferredLanguage) is string language)
            {
                CultureCookie.Write(Response, language);
            }

            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account {Email} locked out", Input.Email);
            ModelState.AddModelError(string.Empty, _localizer["Identity.Login.LockedOut"]);
            return Page();
        }

        ModelState.AddModelError(string.Empty, _localizer["Identity.Login.InvalidCredentials"]);
        return Page();
    }
}
