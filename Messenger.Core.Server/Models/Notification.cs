namespace Messenger.Core.Models;

/// <summary>
/// Уведомление
/// </summary>
public class Notification
{
    /// <summary>
    /// Тип уведомления
    /// </summary>
    public NotificationType Type { get; set; }
    
    /// <summary>
    /// Данные уведомления (JSON или текст)
    /// </summary>
    public string Data { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата и время создания
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
