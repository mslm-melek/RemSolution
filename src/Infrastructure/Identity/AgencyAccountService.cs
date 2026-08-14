using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Email;

namespace RemSolution.Infrastructure.Identity;

/// <summary>Provisions the administrator account an agency is opened with.</summary>
public class AgencyAccountService : IAgencyAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILocalizer _localizer;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AgencyAccountService> _logger;

    public AgencyAccountService(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILocalizer localizer,
        IOptions<EmailOptions> emailOptions,
        ILogger<AgencyAccountService> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _localizer = localizer;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<AgencyAdminAccountResult> EnsureAdministratorAsync(
        int agencyId, string? agencyName, string? email, string? fullName, CancellationToken cancellationToken)
    {
        // Any user at all, not just an administrator: an agency somebody can already
        // sign in to needs nothing here, and adding an admin to it would widen access.
        if (await _userManager.Users.AnyAsync(u => u.AgencyId == agencyId, cancellationToken))
        {
            return new AgencyAdminAccountResult(AgencyAdminOutcome.AlreadyProvisioned, AgencyName: agencyName);
        }

        var address = Trimmed(email);

        if (address is null)
        {
            return new AgencyAdminAccountResult(AgencyAdminOutcome.NoEmail, AgencyName: agencyName);
        }

        var existing = await _userManager.FindByEmailAsync(address);

        if (existing is not null)
        {
            _logger.LogWarning(
                "Agency {AgencyId} asked for an administrator on {Email}, which already belongs to user {UserId}.",
                agencyId, address, existing.Id);

            return new AgencyAdminAccountResult(
                AgencyAdminOutcome.EmailTaken, existing.Id, address, AgencyName: agencyName);
        }

        var user = new ApplicationUser
        {
            // The login IS the email throughout this app.
            UserName = address,
            Email = address,
            FullName = Trimmed(fullName),
            AgencyId = agencyId,
            // We chose the password, so the account can only replace it until it does.
            MustChangePassword = true,
            // The onboarding culture is the only signal available; they can change it.
            PreferredLanguage = Languages.Normalize(CultureInfo.CurrentUICulture.Name) ?? Languages.Default,
        };

        var password = TemporaryPassword.Generate();

        var created = await _userManager.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            // Throwing rolls the caller's transaction back, agency included.
            throw new InvalidOperationException(
                $"Could not create the administrator account for '{address}': {Describe(created)}");
        }

        var roled = await _userManager.AddToRoleAsync(user, Roles.AgencyAdministrator);

        if (!roled.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the AgencyAdministrator role to '{address}': {Describe(roled)}");
        }

        _logger.LogInformation(
            "Provisioned administrator account {UserId} for agency {AgencyId}.", user.Id, agencyId);

        return new AgencyAdminAccountResult(
            AgencyAdminOutcome.Created, user.Id, address, user.FullName, password, agencyName);
    }

    public async Task<bool> SendWelcomeAsync(AgencyAdminAccountResult account, CancellationToken cancellationToken)
    {
        if (!account.HasCredentials || string.IsNullOrWhiteSpace(account.Email))
        {
            return false;
        }

        try
        {
            var agency = string.IsNullOrWhiteSpace(account.AgencyName)
                ? _emailOptions.FromName
                : account.AgencyName;

            await _emailSender.SendEmailAsync(
                account.Email,
                _localizer["Email.AgencyAdmin.Subject", agency],
                BuildBody(account, agency));

            return true;
        }
        catch (Exception exception)
        {
            // The caller holds the same credentials, so a dead mail server is not fatal.
            _logger.LogError(
                exception,
                "Could not email the credentials for administrator account {UserId}; they were returned to the caller instead.",
                account.UserId);

            return false;
        }
    }

    private string BuildBody(AgencyAdminAccountResult account, string agency)
    {
        var encode = HtmlEncoder.Default;

        var body = new StringBuilder();

        body.Append("<p>").Append(encode.Encode(
            _localizer["Email.AgencyAdmin.Hello", account.FullName ?? account.Email!])).Append("</p>");

        body.Append("<p>").Append(encode.Encode(
            _localizer["Email.AgencyAdmin.Created", agency])).Append("</p>");

        body.Append("<p><strong>").Append(encode.Encode(_localizer["Email.AgencyAdmin.Login"]))
            .Append("</strong>: ").Append(encode.Encode(account.Email!)).Append("<br>");

        body.Append("<strong>").Append(encode.Encode(_localizer["Email.AgencyAdmin.Password"]))
            .Append("</strong>: <code>").Append(encode.Encode(account.TemporaryPassword!)).Append("</code></p>");

        body.Append("<p>").Append(encode.Encode(_localizer["Email.AgencyAdmin.MustChange"])).Append("</p>");

        // Only when the deployment knows its own public address (never the request's).
        if (SignInUrl() is string url)
        {
            body.Append("<p><a href=\"").Append(encode.Encode(url)).Append("\">")
                .Append(encode.Encode(_localizer["Email.AgencyAdmin.SignIn"])).Append("</a></p>");
        }

        return body.ToString();
    }

    private string? SignInUrl() =>
        string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl)
            ? null
            : $"{_emailOptions.PublicBaseUrl.TrimEnd('/')}/Identity/Account/Login";

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
