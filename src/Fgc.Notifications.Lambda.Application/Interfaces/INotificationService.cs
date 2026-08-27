namespace Fgc.Notifications.Lambda.Application.Interfaces;

public interface INotificationService
{
    Task SendWelcomeEmailAsync(string recipientName, string recipientEmail, CancellationToken cancellationToken = default);

    Task SendPurchaseConfirmationEmailAsync(Guid userId, Guid gameId, decimal price, CancellationToken cancellationToken = default);
}
