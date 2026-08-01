using System.Collections.Concurrent;
using Messenger.Core.Models;
using Messenger.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Messenger.Server.Api.Hubs;
using Messenger.Core.Utils;

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
    
    /// <summary>
    /// Отправить сообщение конкретному пользователю через WebSocket
    /// </summary>
    public async Task SendMessageToUserAsync(int recipientId, string content, int senderId, string senderName, int messageId = 0)
    {
        // Формат сообщения согласно ТЗ
        var messageData = new
        {
            type = "message",
            id = messageId,
            senderId = senderId,
            senderName = senderName,
            text = content,
            timestamp = DateTime.UtcNow.ToString("o"),
            isDeleted = false
        };
        
        await _hubContext.Clients.Group($"user_{recipientId}").SendAsync("message", messageData);
        
        ConsoleLogger.Info($"[WebSocket Message] От {senderName} пользователю {recipientId}: {content}");
    }
    
    /// <summary>
    /// Отправить уведомление всем подключенным клиентам (broadcast)
    /// </summary>
    public async Task SendNotificationAsync(Notification notification)
    {
        // Отправка уведомления всем подключенным клиентам
        await _hubContext.Clients.All.SendAsync("NotificationReceived", 
            notification.Type.ToString(), 
            notification.Data);
        
        ConsoleLogger.Info($"[Notification] {notification.Type}: {notification.Data}");
    }
    
    /// <summary>
    /// Отправить уведомление конкретному пользователю
    /// </summary>
    public async Task SendNotificationToUserAsync(int userId, Notification notification)
    {
        // Отправка уведомления конкретному пользователю
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationReceived",
            notification.Type.ToString(),
            notification.Data);
        
        ConsoleLogger.Info($"[Notification to User {userId}] {notification.Type}: {notification.Data}");
    }
}
