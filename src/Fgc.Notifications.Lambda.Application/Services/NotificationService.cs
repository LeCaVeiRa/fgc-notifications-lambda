using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Fgc.Notifications.Lambda.Application.Services;

// Mensagens de log abaixo replicam, literalmente, as que o fgc-notifications-api loga em
// UserCreatedEventConsumer/PaymentProcessedEventConsumer - mantidas em paridade de propósito.
public class NotificationService(INotificationRepository repository, IEmailSender emailSender, ILogger<NotificationService> logger) : INotificationService
{
    public async Task SendWelcomeEmailAsync(string recipientName, string recipientEmail, CancellationToken cancellationToken = default)
    {
        var notification = Notification.CreateWelcome(recipientName, recipientEmail);

        await emailSender.SendAsync(notification.RecipientEmail, notification.RecipientName, notification.Subject, notification.Body, cancellationToken);
        await repository.AddAsync(notification, cancellationToken);

        logger.LogInformation(
            "📧 [WELCOME EMAIL] Para: {email} | Assunto: Bem-vindo à FCG, {userName}! | Corpo: Olá {userName}, sua conta foi criada com sucesso! Aproveite nosso catálogo de jogos educativos.",
            recipientEmail,
            recipientName,
            recipientName);
    }

    public async Task SendPurchaseConfirmationEmailAsync(Guid userId, Guid gameId, decimal price, CancellationToken cancellationToken = default)
    {
        var notification = Notification.CreatePurchaseConfirmation(userId, gameId, price);

        await emailSender.SendAsync(notification.RecipientEmail, notification.RecipientName, notification.Subject, notification.Body, cancellationToken);
        await repository.AddAsync(notification, cancellationToken);

        logger.LogInformation(
            "📧 [PURCHASE CONFIRMATION] Para: {userId} | Assunto: Compra confirmada! | Corpo: Sua compra de {price:C2} foi aprovada. GameId: {gameId}",
            userId,
            price,
            gameId);
    }
}
