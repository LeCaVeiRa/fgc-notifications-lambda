namespace Fgc.Notifications.Lambda.Application.DTOs;

public record NotificationResponse(
    Guid Id,
    string Type,
    string RecipientName,
    string RecipientEmail,
    string Subject,
    DateTime SentAt);
