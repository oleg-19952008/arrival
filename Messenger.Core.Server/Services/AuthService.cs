using Messenger.Core.Models;
using Messenger.Core.Interfaces;
namespace Messenger.Core.Services;

/// <summary>
/// Сервис аутентификации (серверная реализация с БД)
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly string _jwtSecret;
    
    public AuthService(
        IUserRepository userRepository,
        INotificationService notificationService,
        string jwtSecret = "YourSuperSecretKeyForMessengerCoreServer2024!")
    {
        _userRepository = userRepository;
        _notificationService = notificationService;
        _jwtSecret = jwtSecret;
    }
    
    public async Task<OperationResult> RegisterAsync(string username, string password)
    {
        // Проверка наличия пользователя
        var existingUser = await _userRepository.GetByUsernameAsync(username);
        if (existingUser != null)
        {
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь с таким именем уже существует" 
            };
        }
        
        // Хеширование пароля
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        // Создание нового пользователя
        var newUser = new User
        {
            Username = username,
            PasswordHash = passwordHash,
            Role = UserRole.User,
            Status = UserStatus.Pending, // Требуется одобрение администратора
            CreatedAt = DateTime.UtcNow
        };
        
        await _userRepository.AddAsync(newUser);
        
        return new OperationResult { Success = true };
    }
    
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        // Поиск пользователя
        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
        {
            return new AuthResult 
            { 
                Success = false, 
                ErrorMessage = "Неверное имя пользователя или пароль" 
            };
        }
        
        // Проверка статуса
        if (user.Status == UserStatus.Blocked)
        {
            return new AuthResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь заблокирован" 
            };
        }
        
        if (user.Status == UserStatus.Pending)
        {
            return new AuthResult 
            { 
                Success = false, 
                ErrorMessage = "Пользователь ожидает одобрения администратора" 
            };
        }
        
        // Проверка пароля
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return new AuthResult 
            { 
                Success = false, 
                ErrorMessage = "Неверное имя пользователя или пароль" 
            };
        }
        
        // Обновление времени последнего входа
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        
        // Генерация JWT токена
        var token = GenerateJwtToken(user);
        
        // Уведомление о входе
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.UserLoggedIn,
            Data = $"Пользователь {user.Username} вошёл в систему",
            CreatedAt = DateTime.UtcNow
        });
        
        return new AuthResult 
        { 
            Success = true, 
            Token = token,
            User = user
        };
    }
    
    public async Task LogoutAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            // Уведомление о выходе
            await _notificationService.SendNotificationAsync(new Notification
            {
                Type = NotificationType.UserLoggedOut,
                Data = $"Пользователь {user.Username} вышел из системы",
                CreatedAt = DateTime.UtcNow
            });
        }
    }
    
    private string GenerateJwtToken(User user)
    {
        var key = System.Text.Encoding.UTF8.GetBytes(_jwtSecret);
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.Username),
            new System.Security.Claims.Claim("Role", user.Role.ToString()),
            new System.Security.Claims.Claim("Status", user.Status.ToString())
        };
        
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7), // 7 дней как в ТЗ
            SigningCredentials = credentials
        };
        
        var handler = new Microsoft.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        
        return handler.WriteToken(token);
    }
}
