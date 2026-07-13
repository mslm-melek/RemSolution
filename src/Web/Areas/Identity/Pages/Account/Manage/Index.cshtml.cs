using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string? Email { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [StringLength(200)]
        [Display(Name = "Full name")]
        public string? FullName { get; set; }

        [Phone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
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
            PhoneNumber = await _userManager.GetPhoneNumberAsync(user)
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

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your profile has been updated.";
        return RedirectToPage();
    }
}
