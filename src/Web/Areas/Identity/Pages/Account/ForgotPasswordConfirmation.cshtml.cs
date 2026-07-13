using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RemSolution.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordConfirmationModel : PageModel
{
    public string? DevResetLink { get; private set; }

    public void OnGet()
    {
        DevResetLink = TempData["DevResetLink"] as string;
    }
}
