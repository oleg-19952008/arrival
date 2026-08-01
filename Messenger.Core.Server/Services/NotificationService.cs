using System.Collections.Concurrent;
using Messenger.Core.Models;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис уведомлений (серверная реализация с WebSocket)
/// </summary>
public class NotificationService : INotificationService
{
    // Хранилище подключенных клиентов: userId -> connection
    private readonly ConcurrentDictionary<int, object> _connectedClients = new();
    
    // Хранилище всех подключений для широковещательной рассылки
    private readonly List<object> _allConnections = new();
    
    public Task SendNotificationAsync(Notification notification)
    {
        // В реальной реализации здесь будет отправка через WebSocket всем подключенным клиентам
        // Для пока заглушка - логирование
        Console.WriteLine($"[Notification] {notification.Type}: {notification.Data}");
        
        return Task.CompletedTask;
    }
    
    public Task SendNotificationToUserAsync(int userId, Notification notification)
    {
        // В реальной реализации здесь будет отправка через WebSocket конкретному пользователю
        Console.WriteLine($"[Notification to User {userId}] {notification.Type}: {notification.Data}");
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Регистрация подключенного клиента (для использования с WebSocket)
    /// </summary>
    public void RegisterClient(int userId, object connection)
    {
        _connectedClients.TryAdd(userId, connection);
        lock (_allConnections)
        {
            _allConnections.Add(connection);
        }
    }
    
    /// <summary>
    /// Удаление подключенного клиента
    /// </summary>
    public void UnregisterClient(int userId)
    {
        if (_connectedClients.TryRemove(userId, out var connection))
        {
            lock (_allConnections)
            {
                _allConnections.Remove(connection);
            }
        }
    }
}
