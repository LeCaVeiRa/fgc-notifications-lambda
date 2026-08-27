namespace Fgc.Notifications.Lambda.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string body, CancellationToken cancellationToken = default);
}
