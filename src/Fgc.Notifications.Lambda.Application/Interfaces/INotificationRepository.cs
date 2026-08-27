using Fgc.Notifications.Lambda.Domain.Entities;

namespace Fgc.Notifications.Lambda.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
}
