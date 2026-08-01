using Messenger.Core.Models;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис пользователей (серверная реализация с БД)
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    
    public UserService(
        IUserRepository userRepository,
        INotificationService notificationService)
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
    }
    
    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }
    
    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _userRepository.GetByIdAsync(id);
    }
    
    public async Task<OperationResult> UpdateUserStatusAsync(int userId, UserStatus status)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь не найден" 
            };
        }
        
        user.Status = status;
        await _userRepository.UpdateAsync(user);
        
        // Уведомление об изменении статуса
        var notificationType = status switch
        {
            UserStatus.Blocked => NotificationType.UserBlocked,
            UserStatus.Active => NotificationType.UserUnblocked,
            _ => NotificationType.UserUnblocked
        };
        
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = notificationType,
            Data = $"Статус пользователя {user.Username} изменён на {status}",
            CreatedAt = DateTime.UtcNow
        });
        
        return new OperationResult { Success = true };
    }
    
    public async Task<OperationResult> DeleteUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь не найден" 
            };
        }
        
        // Помечаем как заблокированного вместо физического удаления
        user.Status = UserStatus.Blocked;
        await _userRepository.UpdateAsync(user);
        
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.UserBlocked,
            Data = $"Пользователь {user.Username} был удалён",
            CreatedAt = DateTime.UtcNow
        });
        
        return new OperationResult { Success = true };
    }
}
