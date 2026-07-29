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
using ClientEntity = RemSolution.Domain.Entities.Client;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// Provisions and links customer-portal accounts for agency clients. See
/// <see cref="IClientAccountService"/> for why linking and mailing are two
/// calls rather than one.
/// </summary>
public class ClientAccountService : IClientAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenant;
    private readonly IEmailSender _emailSender;
    private readonly ILocalizer _localizer;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<ClientAccountService> _logger;

    public ClientAccountService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        ITenantProvider tenant,
        IEmailSender emailSender,
        ILocalizer localizer,
        IOptions<EmailOptions> emailOptions,
        ILogger<ClientAccountService> logger)
    {
        _userManager = userManager;
        _context = context;
        _tenant = tenant;
        _emailSender = emailSender;
        _localizer = localizer;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public Task<ClientAccountResult> LinkOrCreateAsync(ClientEntity client, CancellationToken cancellationToken) =>
        ProvisionAsync(client, reinvite: false, cancellationToken);

    public Task<ClientAccountResult> ReinviteAsync(ClientEntity client, CancellationToken cancellationToken) =>
        ProvisionAsync(client, reinvite: true, cancellationToken);

    private async Task<ClientAccountResult> ProvisionAsync(
        ClientEntity client, bool reinvite, CancellationToken cancellationToken)
    {
        var email = Trimmed(client.Email);

        if (email is null)
        {
            return ClientAccountResult.Nothing;
        }

        // An already-linked client resolves through the LINK, not the address.
        // The two can disagree — an agency corrects a typo in a contact email
        // months later — and when they do, the account someone already signs in
        // with wins: silently re-pointing a client at a different login (or
        // renaming theirs) because a contact field was edited would be the
        // agency changing a customer's identity by accident.
        if (client.MarketplaceUserId is { Length: > 0 } linkedId)
        {
            var linked = await _userManager.FindByIdAsync(linkedId);

            if (linked is not null)
            {
                return reinvite
                    ? await ReissueAsync(linked, cancellationToken)
                    : Describe(ClientAccountOutcome.AlreadyLinked, linked);
            }

            // The account was deleted out from under the link; fall through and
            // provision a fresh one rather than leaving a dangling reference.
            _logger.LogWarning(
                "Client {ClientId} pointed at missing user {UserId}; re-provisioning.", client.Id, linkedId);
        }

        var existing = await _userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            // Staff and platform logins are never adopted as customer
            // identities — see ClientAccountOutcome.EmailBelongsToStaff.
            if (!await _userManager.IsInRoleAsync(existing, Roles.Customer))
            {
                _logger.LogWarning(
                    "Client {ClientId} email belongs to non-customer account {UserId}; not linking.",
                    client.Id, existing.Id);

                return new ClientAccountResult(ClientAccountOutcome.EmailBelongsToStaff, existing.Id, email);
            }

            client.MarketplaceUserId = existing.Id;

            return reinvite
                ? await ReissueAsync(existing, cancellationToken)
                : Describe(ClientAccountOutcome.Linked, existing);
        }

        return await CreateAsync(client, email, cancellationToken);
    }

    private async Task<ClientAccountResult> CreateAsync(
        ClientEntity client, string email, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = FullNameOf(client),
            // Customers are not tenant-scoped: one account, a Client row per
            // agency they rent from (see Client.MarketplaceUserId).
            AgencyId = null,
            // Nobody has chosen this password but us.
            MustChangePassword = true,
            // The agency's working language is the best guess available for a
            // person who has never opened the app; they can change it after
            // signing in.
            PreferredLanguage = Languages.Normalize(CultureInfo.CurrentUICulture.Name) ?? Languages.Default,
        };

        var password = TemporaryPassword.Generate();

        var created = await _userManager.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            // Throwing rolls the caller's transaction back, which is the honest
            // outcome: the alternative is a client the agency believes has a
            // login and does not. The realistic cause is two requests racing on
            // the same address, and a retry resolves it.
            throw new InvalidOperationException(
                $"Could not create a customer account for '{email}': {Describe(created)}");
        }

        var roled = await _userManager.AddToRoleAsync(user, Roles.Customer);

        if (!roled.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the Customer role to '{email}': {Describe(roled)}");
        }

        client.MarketplaceUserId = user.Id;

        _logger.LogInformation(
            "Provisioned customer account {UserId} for client {ClientId}.", user.Id, client.Id);

        return new ClientAccountResult(
            ClientAccountOutcome.Created, user.Id, email, user.FullName, password);
    }

    private async Task<ClientAccountResult> ReissueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Only an account still holding the password WE generated may be reset
        // from here. Once the customer has chosen their own, this is their
        // identity — possibly shared with other agencies on the marketplace —
        // and an agency clicking "resend" must not be able to lock them out of
        // it. They still have the forgotten-password flow.
        if (!user.MustChangePassword)
        {
            return Describe(ClientAccountOutcome.AlreadyActive, user);
        }

        var password = TemporaryPassword.Generate();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, token, password);

        if (!reset.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not re-issue the temporary password for '{user.Email}': {Describe(reset)}");
        }

        _logger.LogInformation("Re-issued the temporary password for customer account {UserId}.", user.Id);

        return new ClientAccountResult(
            ClientAccountOutcome.PasswordReset, user.Id, user.Email, user.FullName, password);
    }

    public async Task<bool> SendCredentialsAsync(ClientAccountResult result, CancellationToken cancellationToken)
    {
        if (!result.HasCredentials || string.IsNullOrWhiteSpace(result.Email))
        {
            return false;
        }

        try
        {
            var agency = await AgencyNameAsync(cancellationToken);

            await _emailSender.SendEmailAsync(
                result.Email,
                _localizer["Email.ClientAccount.Subject", agency],
                BuildBody(result, agency));

            return true;
        }
        catch (Exception exception)
        {
            // Everything this mail refers to is already committed. Failing the
            // caller now would undo a booking because a mail server was down.
            _logger.LogError(
                exception,
                "Could not email the credentials for customer account {UserId}; the agency can re-send them.",
                result.UserId);

            return false;
        }
    }

    private string BuildBody(ClientAccountResult result, string agency)
    {
        var encode = HtmlEncoder.Default;

        var intro = result.Outcome == ClientAccountOutcome.PasswordReset
            ? _localizer["Email.ClientAccount.Reissued", agency]
            : _localizer["Email.ClientAccount.Created", agency];

        var body = new StringBuilder();

        body.Append("<p>").Append(encode.Encode(
            _localizer["Email.ClientAccount.Hello", result.FullName ?? result.Email!])).Append("</p>");

        body.Append("<p>").Append(encode.Encode(intro)).Append("</p>");

        body.Append("<p><strong>").Append(encode.Encode(_localizer["Email.ClientAccount.Login"]))
            .Append("</strong>: ").Append(encode.Encode(result.Email!)).Append("<br>");

        body.Append("<strong>").Append(encode.Encode(_localizer["Email.ClientAccount.Password"]))
            .Append("</strong>: <code>").Append(encode.Encode(result.TemporaryPassword!)).Append("</code></p>");

        body.Append("<p>").Append(encode.Encode(_localizer["Email.ClientAccount.MustChange"])).Append("</p>");

        // Only when the deployment knows its own public address; see
        // EmailOptions.PublicBaseUrl for why it is never taken from the request.
        if (SignInUrl() is string url)
        {
            body.Append("<p><a href=\"").Append(encode.Encode(url)).Append("\">")
                .Append(encode.Encode(_localizer["Email.ClientAccount.SignIn"])).Append("</a></p>");
        }

        body.Append("<p>").Append(encode.Encode(_localizer["Email.ClientAccount.Ignore"])).Append("</p>");

        return body.ToString();
    }

    private string? SignInUrl() =>
        string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl)
            ? null
            : $"{_emailOptions.PublicBaseUrl.TrimEnd('/')}/Identity/Account/Login";

    private async Task<string> AgencyNameAsync(CancellationToken cancellationToken)
    {
        if (_tenant.AgencyId is not int agencyId)
        {
            return _emailOptions.FromName;
        }

        var name = await _context.Agencies
            .AsNoTracking()
            .Where(a => a.Id == agencyId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(name) ? _emailOptions.FromName : name;
    }

    private static ClientAccountResult Describe(ClientAccountOutcome outcome, ApplicationUser user) =>
        new(outcome, user.Id, user.Email, user.FullName);

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));

    private static string? FullNameOf(ClientEntity client)
    {
        var name = $"{client.FirstName} {client.LastName}".Trim();

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
