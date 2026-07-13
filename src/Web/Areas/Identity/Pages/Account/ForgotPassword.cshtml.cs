using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);

        // Always show the confirmation page, even when the account does not
        // exist, so the form cannot be used to enumerate registered emails.
        // Unlike the default UI we do not require a confirmed email: accounts
        // register without confirmation (RequireConfirmedAccount is off), so
        // that check would make recovery impossible for everyone.
        if (user is not null)
        {
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code, email = Input.Email },
                protocol: Request.Scheme)!;

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Reset your RemSolution password",
                $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            // No SMTP is configured in Development (IEmailSender is a no-op),
            // so surface the link on the confirmation page to keep the flow
            // testable. Never do this outside Development.
            if (_environment.IsDevelopment())
            {
                TempData["DevResetLink"] = callbackUrl;
            }
        }

        return RedirectToPage("./ForgotPasswordConfirmation");
    }
}
