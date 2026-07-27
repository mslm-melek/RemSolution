using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Identity.Validation.Required")]
        [StringLength(200, ErrorMessage = "Identity.Validation.MaxLength")]
        [Display(Name = "Identity.Field.FullName")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identity.Validation.Required")]
        [EmailAddress(ErrorMessage = "Identity.Validation.Email")]
        [Display(Name = "Identity.Field.Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identity.Validation.Required")]
        [StringLength(100, ErrorMessage = "Identity.Validation.PasswordLength", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Identity.Field.ConfirmPassword")]
        [Compare(nameof(Password), ErrorMessage = "Identity.Validation.PasswordMismatch")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FullName = Input.FullName.Trim(),
            // Whatever language the visitor filled this form in becomes the
            // account's preference, so it follows them to their next device
            // instead of only living in this browser's culture cookie.
            PreferredLanguage = Languages.Normalize(CultureInfo.CurrentUICulture.Name) ?? Languages.Default
        };

        var result = await _userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} created a new account with password", Input.Email);

            // Self-registration is the customer marketplace funnel: the new
            // account is a Customer (no agency, no permissions) that can browse
            // available cars and request bookings.
            await _userManager.AddToRoleAsync(user, Roles.Customer);

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
