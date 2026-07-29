using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Areas.Identity.Pages.Account.Manage;

public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<ChangePasswordModel> _logger;

    public ChangePasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStringLocalizer<SharedResource> localizer,
        ILogger<ChangePasswordModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _localizer = localizer;
        _logger = logger;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Identity.Validation.Required")]
        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.CurrentPassword")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identity.Validation.Required")]
        [StringLength(100, ErrorMessage = "Identity.Validation.PasswordLength", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.NewPassword")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.ConfirmNewPassword")]
        [Compare(nameof(NewPassword), ErrorMessage = "Identity.Validation.NewPasswordMismatch")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return NotFound("Unable to load user.");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return NotFound("Unable to load user.");

        var result = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        // Before RefreshSignInAsync, so the ticket it mints below is already
        // free of the MustChangePassword claim (see the extension).
        await _userManager.ClearMustChangePasswordAsync(user);

        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("User changed their password successfully");
        StatusMessage = _localizer["Identity.Manage.PasswordChanged"];

        return RedirectToPage();
    }
}
