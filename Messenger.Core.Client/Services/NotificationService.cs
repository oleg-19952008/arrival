using Messenger.Core.Models;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис уведомлений (базовая реализация без БД - для клиента)
/// </summary>
public class NotificationService : INotificationService
{
    public Task SendNotificationAsync(Notification notification)
    {
        // В клиентской версии отправка уведомлений невозможна без сервера
        return Task.CompletedTask;
    }
    
    public Task SendNotificationToUserAsync(int userId, Notification notification)
    {
        // В клиентской версии отправка уведомлений пользователю невозможна без сервера
        return Task.CompletedTask;
    }
}
