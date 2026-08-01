namespace Messenger.Core.Models;

/// <summary>
/// Пользователь системы
/// </summary>
public class User
{
    /// <summary>
    /// Уникальный идентификатор
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Имя пользователя (логин)
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Хеш пароля
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Роль пользователя
    /// </summary>
    public UserRole Role { get; set; }
    
    /// <summary>
    /// Статус пользователя
    /// </summary>
    public UserStatus Status { get; set; }
    
    /// <summary>
    /// Дата и время создания
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Дата и время последнего входа
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
