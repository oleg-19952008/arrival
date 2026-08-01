namespace Messenger.Core.Models;

/// <summary>
/// Сообщение
/// </summary>
public class Message
{
    /// <summary>
    /// Уникальный идентификатор
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Идентификатор отправителя
    /// </summary>
    public int SenderId { get; set; }
    
    /// <summary>
    /// Имя отправителя
    /// </summary>
    public string? SenderName { get; set; }
    
    /// <summary>
    /// Идентификатор получателя (null для общего чата)
    /// </summary>
    public int? RecipientId { get; set; }
    
    /// <summary>
    /// Тип сообщения
    /// </summary>
    public MessageType Type { get; set; }
    
    /// <summary>
    /// Содержимое сообщения (текст или путь к файлу)
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата и время отправки
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Сообщение удалено
    /// </summary>
    public bool IsDeleted { get; set; }
}
