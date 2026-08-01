using Microsoft.AspNetCore.SignalR;

namespace Messenger.Server.Api.Hubs;

/// <summary>
/// WebSocket хаб для уведомлений в реальном времени
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// Подключение клиента
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        
        await Clients.All.SendAsync("UserConnected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Отключение клиента
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Получить уведомление
    /// </summary>
    public async Task ReceiveNotification(string type, string data)
    {
        await Clients.Caller.SendAsync("NotificationReceived", type, data);
    }
}
