using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Identity;
using RemSolution.Web.Infrastructure;

namespace RemSolution.Web.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
    }

    public string? Email { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [StringLength(200, ErrorMessage = "Identity.Validation.MaxLength")]
        [Display(Name = "Identity.Field.FullName")]
        public string? FullName { get; set; }

        [Phone(ErrorMessage = "Identity.Validation.Phone")]
        [Display(Name = "Identity.Field.PhoneNumber")]
        public string? PhoneNumber { get; set; }

        // Neutral language tag; the <select> only offers Languages.All.
        public string? PreferredLanguage { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return NotFound("Unable to load user.");

        Email = await _userManager.GetEmailAsync(user);
        Input = new InputModel
        {
            FullName = user.FullName,
            PhoneNumber = await _userManager.GetPhoneNumberAsync(user),
            PreferredLanguage = Languages.Normalize(user.PreferredLanguage) ?? Languages.Default
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return NotFound("Unable to load user.");

        Email = await _userManager.GetEmailAsync(user);

        if (!ModelState.IsValid)
            return Page();

        var trimmedName = string.IsNullOrWhiteSpace(Input.FullName) ? null : Input.FullName.Trim();

        if (trimmedName != user.FullName)
        {
            user.FullName = trimmedName;
            var nameResult = await _userManager.UpdateAsync(user);
            if (!nameResult.Succeeded)
            {
                foreach (var error in nameResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
        }

        var currentPhone = await _userManager.GetPhoneNumberAsync(user);

        if (Input.PhoneNumber != currentPhone)
        {
            var result = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
        }

        // Ignore anything not in the supported set rather than 400-ing on a
        // tampered form post; the previous value simply stands.
        if (Languages.Normalize(Input.PreferredLanguage) is string language && language != user.PreferredLanguage)
        {
            user.PreferredLanguage = language;
            await _userManager.UpdateAsync(user);
            // Also the cookie, so the SPA opens in the same language.
            CultureCookie.Write(Response, language);
        }

        // Re-mints the ticket, so the PreferredLanguage claim is current on the
        // very next request rather than after the security-stamp interval.
        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = _localizer["Identity.Manage.ProfileUpdated"];
        return RedirectToPage();
    }
}
