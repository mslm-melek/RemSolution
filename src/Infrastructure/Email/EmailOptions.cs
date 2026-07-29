using System.ComponentModel.DataAnnotations;

namespace RemSolution.Infrastructure.Email;

/// <summary>
/// SMTP settings for outbound mail (password resets, client account
/// invitations). Absent or without a Host, the app registers
/// <see cref="LoggingEmailSender"/> instead and nothing leaves the machine —
/// which is what makes a developer checkout runnable without a mail server.
/// The password belongs in Key Vault / user secrets, never in appsettings.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP host. Empty disables real sending (see the class remarks).</summary>
    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>
    /// STARTTLS / implicit TLS. On by default: the submission ports agencies
    /// will actually use (587, 465) both expect an encrypted session, and
    /// defaulting this off would silently put credentials on the wire.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>Envelope sender. Falls back to <see cref="UserName"/>.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Display name shown to the recipient.</summary>
    public string FromName { get; set; } = "RemSolution";

    /// <summary>
    /// Public base URL of this deployment ("https://app.example.com"), used to
    /// build the sign-in link inside emails. Requests cannot supply it: a mail
    /// is often sent from a background job with no HttpContext, and taking the
    /// host from a request header would let a spoofed Host header rewrite the
    /// link in somebody else's mail.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
