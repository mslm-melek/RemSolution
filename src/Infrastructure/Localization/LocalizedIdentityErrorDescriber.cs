using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace RemSolution.Infrastructure.Localization;

/// <summary>
/// Translates the ASP.NET Identity failure messages ("Email 'x' is already
/// taken", "Passwords must have at least one digit") that the Register,
/// ChangePassword and ResetPassword pages surface verbatim through
/// <c>error.Description</c>. Without this they stay English on an otherwise
/// translated page.
/// <para>
/// Only the errors this app can actually produce are overridden; anything else
/// falls through to the framework's English default.
/// </para>
/// </summary>
public class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    private IdentityError Error(string code, string key, params object[] arguments) =>
        new() { Code = code, Description = _localizer[key, arguments] };

    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Identity.Error.DefaultError");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Identity.Error.ConcurrencyFailure");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Identity.Error.PasswordMismatch");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "Identity.Error.InvalidToken");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Identity.Error.DuplicateUserName", userName);

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Identity.Error.DuplicateEmail", email);

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Identity.Error.InvalidUserName", userName ?? string.Empty);

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Identity.Error.InvalidEmail", email ?? string.Empty);

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), "Identity.Error.PasswordTooShort", length);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Identity.Error.PasswordRequiresNonAlphanumeric");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Identity.Error.PasswordRequiresDigit");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Identity.Error.PasswordRequiresLower");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Identity.Error.PasswordRequiresUpper");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), "Identity.Error.PasswordRequiresUniqueChars", uniqueChars);

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "Identity.Error.UserAlreadyHasPassword");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "Identity.Error.UserLockoutNotEnabled");
}
