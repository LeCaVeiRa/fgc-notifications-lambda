namespace Fgc.Notifications.Lambda.Application.Events;

public record PaymentProcessedEvent(Guid OrderedId, Guid UserId, Guid GameId, decimal Price, string Status, DateTime ProcessedAt);
