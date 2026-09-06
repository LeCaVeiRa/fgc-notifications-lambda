using Fgc.Notifications.Lambda.Application.Interfaces;

namespace Fgc.Notifications.Lambda.Infrastructure.Email;

// "Envio" simulado de e-mail - não loga por si só; o log de paridade com o fgc-notifications-api
// é responsabilidade do NotificationService, que conhece o tipo de evento sendo processado.
public class LoggingEmailSender : IEmailSender
{
    public Task SendAsync(string toEmail, string toName, string subject, string body, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
