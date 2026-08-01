using System.Collections.Concurrent;
using Messenger.Core.Models;
using Messenger.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Messenger.Server.Api.Hubs;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис уведомлений (серверная реализация с WebSocket через SignalR)
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    
    public NotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }
    
    public async Task SendNotificationAsync(Notification notification)
    {
        // Отправка уведомления всем подключенным клиентам
        await _hubContext.Clients.All.SendAsync("NotificationReceived", 
            notification.Type.ToString(), 
            notification.Data);
        
        Console.WriteLine($"[Notification] {notification.Type}: {notification.Data}");
    }
    
    public async Task SendNotificationToUserAsync(int userId, Notification notification)
    {
        // Отправка уведомления конкретному пользователю
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationReceived",
            notification.Type.ToString(),
            notification.Data);
        
        Console.WriteLine($"[Notification to User {userId}] {notification.Type}: {notification.Data}");
    }
    
    /// <summary>
    /// Отправить сообщение конкретному пользователю через WebSocket
    /// </summary>
    public async Task SendMessageToUserAsync(int recipientId, string content, int senderId, string senderName)
    {
        await _hubContext.Clients.Group($"user_{recipientId}").SendAsync("MessageReceived", new
        {
            senderId,
            senderName,
            content,
            receivedAt = DateTime.UtcNow
        });
        
        Console.WriteLine($"[WebSocket Message] От {senderName} пользователю {recipientId}: {content}");
    }
}
