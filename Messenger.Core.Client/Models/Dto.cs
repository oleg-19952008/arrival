namespace Messenger.Core.Models;

/// <summary>
/// Результат аутентификации
/// </summary>
public class AuthResult
{
    /// <summary>
    /// Успешна ли аутентификация
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// JWT токен
    /// </summary>
    public string? Token { get; set; }
    
    /// <summary>
    /// Информация о пользователе
    /// </summary>
    public User? User { get; set; }
    
    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Результат операции
/// </summary>
public class OperationResult
{
    /// <summary>
    /// Успешна ли операция
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string? ErrorMessage { get; set; }
}
