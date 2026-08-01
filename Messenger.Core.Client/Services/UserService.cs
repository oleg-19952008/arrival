using Messenger.Core.Models;
using Messenger.Core.Interfaces;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис пользователей (базовая реализация без БД - для клиента)
/// </summary>
public class UserService : IUserService
{
    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        // В клиентской версии получение списка пользователей невозможно без сервера
        return Task.FromResult<IEnumerable<User>>(Enumerable.Empty<User>());
    }
    
    public Task<User?> GetUserByIdAsync(int id)
    {
        // В клиентской версии получение пользователя невозможно без сервера
        return Task.FromResult<User?>(null);
    }
    
    public Task<OperationResult> UpdateUserStatusAsync(int userId, UserStatus status)
    {
        // В клиентской версии обновление статуса невозможно без сервера
        return Task.FromResult(new OperationResult 
        { 
            Success = false, 
            ErrorMessage = "Управление пользователями возможно только через сервер" 
        });
    }
    
    public Task<OperationResult> DeleteUserAsync(int userId)
    {
        // В клиентской версии удаление пользователя невозможно без сервера
        return Task.FromResult(new OperationResult 
        { 
            Success = false, 
            ErrorMessage = "Удаление пользователя возможно только через сервер" 
        });
    }
}
