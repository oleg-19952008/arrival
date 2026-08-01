using Messenger.Core.Models;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис аутентификации (базовая реализация без БД - для клиента)
/// </summary>
public class AuthService : IAuthService
{
    private readonly INotificationService _notificationService;
    
    public AuthService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }
    
    public Task<OperationResult> RegisterAsync(string username, string password)
    {
        // В клиентской версии регистрация невозможна без сервера
        return Task.FromResult(new OperationResult 
        { 
            Success = false, 
            ErrorMessage = "Регистрация возможна только через сервер" 
        });
    }
    
    public Task<AuthResult> LoginAsync(string username, string password)
    {
        // В клиентской версии аутентификация невозможна без сервера
        return Task.FromResult(new AuthResult 
        { 
            Success = false, 
            ErrorMessage = "Аутентификация возможна только через сервер" 
        });
    }
    
    public Task LogoutAsync(int userId)
    {
        // Заглушка для клиентской версии
        return Task.CompletedTask;
    }
}
