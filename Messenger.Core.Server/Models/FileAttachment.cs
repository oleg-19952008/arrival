namespace Messenger.Core.Models;

/// <summary>
/// Вложение файла к сообщению
/// </summary>
public class FileAttachment
{
    /// <summary>
    /// Уникальный идентификатор
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Идентификатор сообщения
    /// </summary>
    public int MessageId { get; set; }
    
    /// <summary>
    /// Уникальный идентификатор файла (UUID v4)
    /// </summary>
    public string FileId { get; set; } = string.Empty;
    
    /// <summary>
    /// Оригинальное имя файла
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Путь к файлу на сервере
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// MIME тип файла
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Дата и время загрузки
    /// </summary>
    public DateTime UploadedAt { get; set; }
}
