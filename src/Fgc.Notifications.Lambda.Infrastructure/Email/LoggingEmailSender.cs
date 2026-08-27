using Fgc.Notifications.Lambda.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Fgc.Notifications.Lambda.Infrastructure.Email;

// Equivalente ao ConsoleEmailSender do container antigo: "envio" simulado, agora capturado
// pelo CloudWatch Logs em vez do stdout do container - sem mudança de comportamento.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string toName, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[EMAIL SIMULADO] Para: {ToName} <{ToEmail}> | Assunto: {Subject}\n{Body}",
            toName, toEmail, subject, body);

        return Task.CompletedTask;
    }
}
