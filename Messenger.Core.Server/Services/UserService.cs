using Messenger.Core.Models;
using Messenger.Core.Interfaces;
using Messenger.Core.Utils;

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
            ConsoleLogger.Warn($"UpdateUserStatus failed: user with ID {userId} not found");
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
        
        ConsoleLogger.Info($"User '{user.Username}' status changed to {status}");
        
        return new OperationResult { Success = true };
    }
    
    public async Task<OperationResult> DeleteUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            ConsoleLogger.Warn($"DeleteUser failed: user with ID {userId} not found");
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
        
        ConsoleLogger.Info($"User '{user.Username}' marked as deleted (blocked)");
        
        return new OperationResult { Success = true };
    }
    
    public async Task<OperationResult> UnbanUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            ConsoleLogger.Warn($"UnbanUser failed: user with ID {userId} not found");
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь не найден" 
            };
        }
        
        if (user.Status != UserStatus.Blocked)
        {
            ConsoleLogger.Warn($"UnbanUser failed: user '{user.Username}' is not blocked");
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь не заблокирован" 
            };
        }
        
        user.Status = UserStatus.Active;
        await _userRepository.UpdateAsync(user);
        
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.UserUnblocked,
            Data = $"Пользователь {user.Username} был разблокирован",
            CreatedAt = DateTime.UtcNow
        });
        
        ConsoleLogger.Info($"User '{user.Username}' has been unbanned");
        
        return new OperationResult { Success = true };
    }
    
    public async Task<OperationResult> ChangePasswordAsync(int userId, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            ConsoleLogger.Warn($"ChangePassword failed: user with ID {userId} not found");
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь не найден" 
            };
        }
        
        var passwordHasher = new PasswordHasher();
        user.PasswordHash = passwordHasher.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.UserUnblocked,
            Data = $"Пароль пользователя {user.Username} был изменён",
            CreatedAt = DateTime.UtcNow
        });
        
        ConsoleLogger.Info($"Password changed for user '{user.Username}'");
        
        return new OperationResult { Success = true };
    }
}
