using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace RemSolution.Application.FunctionalTests;

// Test double for outgoing mail. Accepts every message, as the logging sender a
// dev checkout uses does, and keeps it so a test can assert what was written to
// whom. Tests match on a recipient address of their own, because the singleton
// outlives the per-test database reset.
public sealed class RecordingEmailSender : IEmailSender
{
    public sealed record SentMail(string Email, string Subject, string HtmlBody);

    private static readonly ConcurrentQueue<SentMail> Sent = new();

    public static IReadOnlyList<SentMail> SentTo(string email) =>
        Sent.Where(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        Sent.Enqueue(new SentMail(email, subject, htmlMessage));
        return Task.CompletedTask;
    }
}
