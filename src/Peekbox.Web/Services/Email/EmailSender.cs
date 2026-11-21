namespace Peekbox.Web.Services.Email;

// One email, as plain text.
public sealed record EmailMessage(string To, string Subject, string Body);

// Sends an email (F61). The site only knows this one method, so a real sender (SMTP or a provider's API) can replace the
// stand-in later by changing what is registered in Program.cs, with nothing else touched.
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

// The stand-in (decision 2026-11-22): nothing leaves the server; the email is written to the application log, so it can be
// read on staging and in a terminal. The log then holds the buyer's name, address and email, which are demo data for now;
// a real sender must not log the body.
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email (stand-in, not sent) to {To}: {Subject}\n{Body}", message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
