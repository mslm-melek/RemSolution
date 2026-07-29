using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RemSolution.Infrastructure.Email;

/// <summary>
/// The sender used when no SMTP host is configured. It keeps a checkout
/// runnable without a mail server: in Development the whole message — including
/// the temporary password or reset link — goes to the log so the flow can be
/// completed by reading the console.
///
/// Outside Development the body is deliberately withheld and each send is a
/// warning: reaching this class in production means mail is misconfigured and
/// customers are not receiving their credentials, and the answer to that is to
/// configure SMTP, not to spill passwords into a shipped log sink.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(IHostEnvironment environment, ILogger<LoggingEmailSender> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "[dev] Email not sent (no SMTP configured). To: {Recipient} | Subject: {Subject}\n{Body}",
                email, subject, htmlMessage);
        }
        else
        {
            _logger.LogWarning(
                "Email {Subject} to {Recipient} was NOT sent: no SMTP host is configured (Email:Host).",
                subject, email);
        }

        return Task.CompletedTask;
    }
}
