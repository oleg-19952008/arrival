using Messenger.Core.Models;

namespace Messenger.Core.Interfaces;

/// <summary>
/// Репозиторий пользователей (только для серверной версии)
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Получить всех пользователей
    /// </summary>
    Task<IEnumerable<User>> GetAllAsync();
    
    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    Task<User?> GetByIdAsync(int id);
    
    /// <summary>
    /// Получить пользователя по имени
    /// </summary>
    Task<User?> GetByUsernameAsync(string username);
    
    /// <summary>
    /// Добавить пользователя
    /// </summary>
    Task<User> AddAsync(User user);
    
    /// <summary>
    /// Обновить пользователя
    /// </summary>
    Task<User> UpdateAsync(User user);
    
    /// <summary>
    /// Удалить пользователя
    /// </summary>
    Task DeleteAsync(int id);
}

/// <summary>
/// Репозиторий сообщений (только для серверной версии)
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Получить все сообщения
    /// </summary>
    Task<IEnumerable<Message>> GetAllAsync();
    
    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    Task<Message?> GetByIdAsync(int id);
    
    /// <summary>
    /// Добавить сообщение
    /// </summary>
    Task<Message> AddAsync(Message message);
    
    /// <summary>
    /// Удалить сообщение
    /// </summary>
    Task DeleteAsync(int id);
    
    /// <summary>
    /// Пометить сообщение как удалённое
    /// </summary>
    Task MarkAsDeletedAsync(int id);
}
