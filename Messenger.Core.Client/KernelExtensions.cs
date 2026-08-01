namespace Messenger.Core;

using Microsoft.Extensions.DependencyInjection;
using Messenger.Core.Interfaces;
using Messenger.Core.Services;

/// <summary>
/// Класс расширения для регистрации сервисов ядра
/// </summary>
public static class KernelExtensions
{
    /// <summary>
    /// Регистрация клиентских сервисов ядра (без поддержки БД)
    /// </summary>
    public static IServiceCollection AddClientKernel(this IServiceCollection services)
    {
        // Сервисы-заглушки для клиента
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IMessageService, MessageService>();
        
        return services;
    }
}
