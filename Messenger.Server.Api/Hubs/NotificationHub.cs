using Microsoft.AspNetCore.SignalR;

namespace Messenger.Server.Api.Hubs;

/// <summary>
/// WebSocket хаб для уведомлений и сообщений в реальном времени
/// </summary>
public class NotificationHub : Hub
{
    // Хранилище подключений: userId -> connectionId
    private static readonly Dictionary<int, string> UserConnections = new();
    
    /// <summary>
    /// Подключение клиента
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var uid))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{uid}");
            lock (UserConnections)
            {
                UserConnections[uid] = Context.ConnectionId;
            }
            Console.WriteLine($"[WebSocket] Пользователь {uid} подключился. ConnectionId: {Context.ConnectionId}");
        }
        
        await Clients.All.SendAsync("UserConnected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Отключение клиента
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Удаляем пользователя из списка подключений
        lock (UserConnections)
        {
            var userToRemove = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (userToRemove != 0)
            {
                UserConnections.Remove(userToRemove);
                Console.WriteLine($"[WebSocket] Пользователь {userToRemove} отключился.");
            }
        }
        
        await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Отправить личное сообщение через WebSocket
    /// </summary>
    public async Task SendMessage(int recipientId, string content, int senderId, string senderName)
    {
        // Отправляем сообщение получателю
        await Clients.Group($"user_{recipientId}").SendAsync("MessageReceived", new
        {
            senderId,
            senderName,
            content,
            receivedAt = DateTime.UtcNow
        });
        
        Console.WriteLine($"[WebSocket] Сообщение от {senderName} отправлено пользователю {recipientId}");
    }

    /// <summary>
    /// Получить уведомление
    /// </summary>
    public async Task ReceiveNotification(string type, string data)
    {
        await Clients.Caller.SendAsync("NotificationReceived", type, data);
    }
}
