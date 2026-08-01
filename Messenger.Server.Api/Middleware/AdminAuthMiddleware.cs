using Microsoft.AspNetCore.Http;
using System.Net;

namespace Messenger.Server.Api.Middleware;

/// <summary>
/// Middleware для проверки localhost доступа к админ-панели
/// </summary>
public class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Проверка что запрос на порт 228 (админка) только с localhost
        var connectionInfo = context.Connection;
        
        // Разрешаем только localhost подключения
        if (!IsLocalhost(connectionInfo.RemoteIpAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Доступ разрешён только с localhost");
            return;
        }

        await _next(context);
    }

    private bool IsLocalhost(IPAddress? ipAddress)
    {
        if (ipAddress == null)
            return false;

        // IPv4 localhost
        if (ipAddress.Equals(IPAddress.Loopback))
            return true;

        // IPv6 localhost
        if (ipAddress.Equals(IPAddress.IPv6Loopback))
            return true;

        return false;
    }
}
