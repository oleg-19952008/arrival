namespace Messenger.Core.Models;

/// <summary>
/// Статус пользователя в системе
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// Ожидает одобрения администратором
    /// </summary>
    PendingApproval = 0,
    
    /// <summary>
    /// Активный пользователь
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Заблокирован
    /// </summary>
    Banned = 2,
    
    /// <summary>
    /// Удалён
    /// </summary>
    Deleted = 3
}

/// <summary>
/// Роль пользователя
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Обычный пользователь
    /// </summary>
    User = 0,
    
    /// <summary>
    /// Администратор
    /// </summary>
    Admin = 1
}

/// <summary>
/// Тип сообщения
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Текстовое сообщение
    /// </summary>
    Text = 0,
    
    /// <summary>
    /// Файл
    /// </summary>
    File = 1
}

/// <summary>
/// Тип уведомления
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Новое сообщение
    /// </summary>
    NewMessage = 0,
    
    /// <summary>
    /// Сообщение удалено
    /// </summary>
    MessageDeleted = 1,
    
    /// <summary>
    /// Пользователь вошёл в систему
    /// </summary>
    UserLoggedIn = 2,
    
    /// <summary>
    /// Пользователь вышел из системы
    /// </summary>
    UserLoggedOut = 3,
    
    /// <summary>
    /// Пользователь заблокирован
    /// </summary>
    UserBanned = 4,
    
    /// <summary>
    /// Пользователь разблокирован
    /// </summary>
    UserUnbanned = 5
}
