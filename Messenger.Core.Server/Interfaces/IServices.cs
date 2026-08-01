using Messenger.Core.Models;

namespace Messenger.Core.Interfaces;

/// <summary>
/// Сервис аутентификации
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Регистрация нового пользователя
    /// </summary>
    Task<OperationResult> RegisterAsync(string username, string password);
    
    /// <summary>
    /// Аутентификация пользователя
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);
    
    /// <summary>
    /// Выход пользователя
    /// </summary>
    Task LogoutAsync(int userId);
}

/// <summary>
/// Сервис пользователей
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Получить всех пользователей
    /// </summary>
    Task<IEnumerable<User>> GetAllUsersAsync();
    
    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    Task<User?> GetUserByIdAsync(int id);
    
    /// <summary>
    /// Обновить статус пользователя
    /// </summary>
    Task<OperationResult> UpdateUserStatusAsync(int userId, UserStatus status);
    
    /// <summary>
    /// Удалить пользователя
    /// </summary>
    Task<OperationResult> DeleteUserAsync(int userId);
    
    /// <summary>
    /// Разблокировать пользователя (Banned → Active)
    /// </summary>
    Task<OperationResult> UnbanUserAsync(int userId);
    
    /// <summary>
    /// Сменить пароль пользователя
    /// </summary>
    Task<OperationResult> ChangePasswordAsync(int userId, string newPassword);
}

/// <summary>
/// Сервис сообщений
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Отправить сообщение
    /// </summary>
    Task<Message> SendMessageAsync(int senderId, MessageType type, string content, int? recipientId = null);
    
    /// <summary>
    /// Получить все сообщения
    /// </summary>
    Task<IEnumerable<Message>> GetAllMessagesAsync();
    
    /// <summary>
    /// Получить сообщения для пользователя (личные + общие)
    /// </summary>
    Task<IEnumerable<Message>> GetMessagesForUserAsync(int userId);
    
    /// <summary>
    /// Удалить сообщение
    /// </summary>
    Task<OperationResult> DeleteMessageAsync(int messageId);
}

/// <summary>
/// Сервис уведомлений
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Отправить уведомление всем подключенным клиентам
    /// </summary>
    Task SendNotificationAsync(Notification notification);
    
    /// <summary>
    /// Отправить уведомление конкретному пользователю
    /// </summary>
    Task SendNotificationToUserAsync(int userId, Notification notification);
}

/// <summary>
/// Сервис файлов
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Загрузить файл
    /// </summary>
    Task<FileAttachment?> UploadFileAsync(Stream fileStream, string fileName, string contentType, int messageId);
    
    /// <summary>
    /// Получить файл по ID
    /// </summary>
    Task<FileAttachment?> GetFileAsync(int fileId);
    
    /// <summary>
    /// Скачать файл
    /// </summary>
    Task<(Stream FileStream, string ContentType, string FileName)?> DownloadFileAsync(int fileId);
    
    /// <summary>
    /// Удалить файл
    /// </summary>
    Task<bool> DeleteFileAsync(int fileId);
}
