using Fgc.Notifications.Lambda.Application.Interfaces;
using Fgc.Notifications.Lambda.Domain.Entities;

namespace Fgc.Notifications.Lambda.Application.Services;

public class NotificationService(INotificationRepository repository, IEmailSender emailSender) : INotificationService
{
    public async Task SendWelcomeEmailAsync(string recipientName, string recipientEmail, CancellationToken cancellationToken = default)
    {
        var notification = Notification.CreateWelcome(recipientName, recipientEmail);

        await emailSender.SendAsync(notification.RecipientEmail, notification.RecipientName, notification.Subject, notification.Body, cancellationToken);
        await repository.AddAsync(notification, cancellationToken);
    }

    public async Task SendPurchaseConfirmationEmailAsync(Guid userId, Guid gameId, decimal price, CancellationToken cancellationToken = default)
    {
        var notification = Notification.CreatePurchaseConfirmation(userId, gameId, price);

        await emailSender.SendAsync(notification.RecipientEmail, notification.RecipientName, notification.Subject, notification.Body, cancellationToken);
        await repository.AddAsync(notification, cancellationToken);
    }
}
