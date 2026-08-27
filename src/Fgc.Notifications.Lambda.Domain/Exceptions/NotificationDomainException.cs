namespace Fgc.Notifications.Lambda.Domain.Exceptions;

public class NotificationDomainException : Exception
{
    public NotificationDomainException(string message) : base(message)
    {
    }
}
