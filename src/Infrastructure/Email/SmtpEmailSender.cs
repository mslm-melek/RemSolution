using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RemSolution.Infrastructure.Email;

/// <summary>
/// Sends mail over SMTP using the framework's own client — no extra dependency
/// for what is a handful of transactional messages (password resets, client
/// account invitations).
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var from = _options.FromAddress ?? _options.UserName;

        if (string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException(
                "Email:FromAddress (or Email:UserName) must be set when Email:Host is configured.");
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            // Explicitly network rather than the machine's pickup directory:
            // the default varies with the host's IIS/SMTP configuration.
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        // Anonymous relay is legitimate on an internal MTA, so credentials are
        // sent only when configured rather than being required.
        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from, _options.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
        };

        message.To.Add(email);

        await client.SendMailAsync(message);

        // The recipient is logged, the body is not: these messages carry
        // temporary passwords and reset links.
        _logger.LogInformation("Sent email {Subject} to {Recipient}", subject, email);
    }
}
