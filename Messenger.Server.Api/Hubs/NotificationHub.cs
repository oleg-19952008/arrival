using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Messenger.Server.Api.Hubs;

/// <summary>
/// WebSocket хаб для уведомлений и сообщений в реальном времени
/// </summary>
public class NotificationHub : Hub
{
    // Хранилище подключений: userId -> connectionId
    private static readonly Dictionary<int, string> UserConnections = new();
    private readonly string _jwtSecret;
    private readonly ILogger<NotificationHub> _logger;
    
    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
        _jwtSecret = "YourSuperSecretKeyForMessengerCoreServer2024WithMinimum32BytesLength!";
    }
    
    /// <summary>
    /// Подключение клиента
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext?.Request.Query["token"].ToString();
        var userIdParam = httpContext?.Request.Query["userId"].ToString();
        
        // Проверка JWT токена
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("[WebSocket] Connection rejected: No JWT token provided");
            await Clients.Caller.SendAsync("Error", "JWT token required");
            Context.Abort();
            return;
        }
        
        // Валидация токена
        var claimsPrincipal = ValidateJwtToken(token);
        if (claimsPrincipal == null)
        {
            _logger.LogWarning("[WebSocket] Connection rejected: Invalid or expired JWT token");
            await Clients.Caller.SendAsync("Error", "Invalid or expired JWT token");
            Context.Abort();
            return;
        }
        
        // Получаем userId из токена
        var userIdClaim = claimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("[WebSocket] Connection rejected: Invalid userId in token");
            await Clients.Caller.SendAsync("Error", "Invalid userId in token");
            Context.Abort();
            return;
        }
        
        // Проверяем что userId в токене совпадает с userId в query параметре
        if (!string.IsNullOrEmpty(userIdParam) && userIdParam != userId.ToString())
        {
            _logger.LogWarning("[WebSocket] Connection rejected: userId mismatch");
            await Clients.Caller.SendAsync("Error", "userId mismatch");
            Context.Abort();
            return;
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        lock (UserConnections)
        {
            UserConnections[userId] = Context.ConnectionId;
        }
        _logger.LogInformation($"[WebSocket] Пользователь {userId} подключился. ConnectionId: {Context.ConnectionId}");
        
        await Clients.All.SendAsync("UserConnected", new { userId, connectionId = Context.ConnectionId });
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
                _logger.LogInformation($"[WebSocket] Пользователь {userToRemove} отключился.");
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
        
        _logger.LogInformation($"[WebSocket] Сообщение от {senderName} отправлено пользователю {recipientId}");
    }

    /// <summary>
    /// Получить уведомление
    /// </summary>
    public async Task ReceiveNotification(string type, string data)
    {
        await Clients.Caller.SendAsync("NotificationReceived", type, data);
    }
    
    /// <summary>
    /// Валидация JWT токена
    /// </summary>
    private System.Security.Claims.ClaimsPrincipal? ValidateJwtToken(string token)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(_jwtSecret);
            var tokenHandler = new JwtSecurityTokenHandler();
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "Messenger.Server.Api",
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };
            
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[WebSocket] JWT validation error: {ex.Message}");
            return null;
        }
    }
}
