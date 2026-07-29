using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Middleware;

/// <summary>
/// Holds an account that was handed a password by somebody else to exactly one
/// thing: replacing it. A client account is provisioned with a temporary
/// password that travels by email (see IClientAccountService), so between the
/// mail being sent and the customer choosing their own, that password is a
/// credential sitting in a mailbox — the account must not be able to act on it.
///
/// <para>
/// Only <c>/api</c> is guarded. Everything else — the SPA bundle, the Razor
/// Identity pages, the culture switch — has to keep serving, or the user could
/// not load the screen that lets them out of this state.
/// </para>
/// </summary>
public class PasswordChangeRequiredMiddleware
{
    // The narrow set of API calls a blocked user still needs: who am I, what is
    // my profile, what language do I read, and the way out. Compared
    // case-insensitively against the full path.
    private static readonly string[] Allowed =
    {
        "/api/Users/me",
        "/api/Users/me/profile",
        "/api/Users/me/password",
        "/api/Users/me/language",
    };

    private readonly RequestDelegate _next;

    public PasswordChangeRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        ILocalizer localizer)
    {
        if (await IsBlockedAsync(context, userManager))
        {
            await WriteProblemAsync(context, localizer);
            return;
        }

        await _next(context);
    }

    private static async Task<bool> IsBlockedAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        // The overwhelmingly common case, settled without touching the database.
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim(Claims.MustChangePassword, "true"))
        {
            return false;
        }

        if (!context.Request.Path.StartsWithSegments("/api") || IsAllowed(context.Request.Path))
        {
            return false;
        }

        // The claim is minted into the ticket, and the ticket outlives the
        // change: an access token issued before the new password was set still
        // carries the flag for the rest of its 15 minutes. Confirming against
        // the user row means a customer is working the instant they finish
        // changing it, rather than locked out of the app they just unlocked.
        // Only blocked-looking requests pay for the lookup.
        var user = await userManager.GetUserAsync(context.User);

        return user is not null && user.MustChangePassword;
    }

    private static bool IsAllowed(PathString path) =>
        Allowed.Any(allowed => path.Equals(allowed, StringComparison.OrdinalIgnoreCase));

    private static async Task WriteProblemAsync(HttpContext context, ILocalizer localizer)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = localizer["Error.PasswordChangeRequired.Title"],
            Detail = localizer["Error.PasswordChangeRequired.Detail"],
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        };

        // 403 is also plain "not allowed"; the SPA keys on this code to send the
        // user to the change-password screen instead of showing an error.
        problem.Extensions["code"] = "password_change_required";

        await context.Response.WriteAsJsonAsync(problem);
    }
}
