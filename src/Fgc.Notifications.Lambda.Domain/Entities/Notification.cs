using Fgc.Notifications.Lambda.Domain.Enums;
using Fgc.Notifications.Lambda.Domain.Exceptions;

namespace Fgc.Notifications.Lambda.Domain.Entities;

public class Notification
{
    public Guid Id { get; private set; }
    public NotificationType Type { get; private set; }
    public string RecipientName { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    private Notification()
    {
    }

    public static Notification CreateWelcome(string recipientName, string recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new NotificationDomainException("Recipient name is required.");

        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
            throw new NotificationDomainException("A valid recipient email is required.");

        return new Notification
        {
            Id = Guid.NewGuid(),
            Type = NotificationType.Welcome,
            RecipientName = recipientName,
            RecipientEmail = recipientEmail,
            Subject = $"Bem-vindo(a) ao FIAP Game Center, {recipientName}!",
            Body = $"Olá {recipientName}, seja bem-vindo(a) ao FCG! Sua conta ({recipientEmail}) foi criada com sucesso.",
            SentAt = DateTime.UtcNow
        };
    }

    // Reidrata uma notificação já persistida (ex.: linha lida do DynamoDB) - ao contrário dos
    // Create*, não gera novo Id/SentAt nem valida regras de criação.
    public static Notification Restore(Guid id, NotificationType type, string recipientName, string recipientEmail, string subject, string body, DateTime sentAt)
    {
        return new Notification
        {
            Id = id,
            Type = type,
            RecipientName = recipientName,
            RecipientEmail = recipientEmail,
            Subject = subject,
            Body = body,
            SentAt = sentAt
        };
    }

    public static Notification CreatePurchaseConfirmation(Guid userId, Guid gameId, decimal price)
    {
        if (userId == Guid.Empty)
            throw new NotificationDomainException("A valid user id is required.");

        if (gameId == Guid.Empty)
            throw new NotificationDomainException("A valid game id is required.");

        var recipientEmail = $"{userId}@fgc.local";
        var recipientName = $"Usuário {userId}";

        return new Notification
        {
            Id = Guid.NewGuid(),
            Type = NotificationType.PurchaseConfirmation,
            RecipientName = recipientName,
            RecipientEmail = recipientEmail,
            Subject = "Compra confirmada no FIAP Game Center!",
            Body = $"Olá {recipientName}, sua compra do jogo {gameId} no valor de {price:C} foi aprovada.",
            SentAt = DateTime.UtcNow
        };
    }
}
