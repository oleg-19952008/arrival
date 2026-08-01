using Messenger.Core.Models;
using Messenger.Core.Interfaces;
using Messenger.Core.Utils;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис управления файлами
/// </summary>
public class FileService : IFileService
{
    private readonly IFileAttachmentRepository _fileRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly string _uploadFolder;
    
    public FileService(
        IFileAttachmentRepository fileRepository,
        IMessageRepository messageRepository,
        string uploadFolder = "uploads")
    {
        _fileRepository = fileRepository;
        _messageRepository = messageRepository;
        _uploadFolder = uploadFolder;
        
        // Создание папки для загрузок если не существует
        if (!Directory.Exists(_uploadFolder))
        {
            Directory.CreateDirectory(_uploadFolder);
            ConsoleLogger.Info($"Upload folder created: {_uploadFolder}");
        }
    }
    
    public async Task<FileAttachment?> UploadFileAsync(Stream fileStream, string fileName, string contentType, int messageId)
    {
        // Проверка существования сообщения
        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null)
        {
            ConsoleLogger.Warn($"UploadFile failed: message with ID {messageId} not found");
            return null;
        }
        
        // Валидация типа файла (разрешённые MIME типы)
        var allowedContentTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf",
            "text/plain",
            "application/zip",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        
        if (!allowedContentTypes.Contains(contentType))
        {
            ConsoleLogger.Warn($"UploadFile rejected: unsupported content type '{contentType}'");
            return null;
        }
        
        // Ограничение размера файла (10 MB)
        const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        
        if (memoryStream.Length > MaxFileSize)
        {
            ConsoleLogger.Warn($"UploadFile rejected: file size {memoryStream.Length} bytes exceeds limit {MaxFileSize} bytes");
            return null;
        }
        
        memoryStream.Position = 0;
        
        // Генерация уникального имени файла
        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(_uploadFolder, uniqueFileName);
        
        // Сохранение файла на диск
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await memoryStream.CopyToAsync(fs);
        }
        
        // Получение размера файла
        var fileSize = new FileInfo(filePath).Length;
        
        // Создание записи в БД
        var attachment = new FileAttachment
        {
            MessageId = messageId,
            FileId = Guid.NewGuid().ToString(),
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSize = fileSize,
            UploadedAt = DateTime.UtcNow
        };
        
        var result = await _fileRepository.AddAsync(attachment);
        ConsoleLogger.Info($"File '{fileName}' ({fileSize} bytes) uploaded successfully as attachment #{result?.Id} (FileId: {result?.FileId})");
        
        return result;
    }
    
    public async Task<FileAttachment?> GetFileAsync(int fileId)
    {
        return await _fileRepository.GetByIdAsync(fileId);
    }
    
    public async Task<(Stream FileStream, string ContentType, string FileName)?> DownloadFileAsync(int fileId)
    {
        var attachment = await _fileRepository.GetByIdAsync(fileId);
        if (attachment == null || !File.Exists(attachment.FilePath))
        {
            ConsoleLogger.Warn($"DownloadFile failed: file with ID {fileId} not found");
            return null;
        }
        
        var stream = new FileStream(
            attachment.FilePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.Read);
        
        ConsoleLogger.Info($"File '{attachment.FileName}' downloaded (ID: {fileId})");
        
        return (stream, attachment.ContentType, attachment.FileName);
    }
    
    public async Task<bool> DeleteFileAsync(int fileId)
    {
        var attachment = await _fileRepository.GetByIdAsync(fileId);
        if (attachment == null)
        {
            ConsoleLogger.Warn($"DeleteFile failed: file with ID {fileId} not found");
            return false;
        }
        
        // Удаление файла с диска
        if (File.Exists(attachment.FilePath))
        {
            File.Delete(attachment.FilePath);
            ConsoleLogger.Info($"File '{attachment.FileName}' deleted from disk");
        }
        
        // Удаление записи из БД
        await _fileRepository.DeleteAsync(fileId);
        ConsoleLogger.Info($"File attachment #{fileId} deleted from database");
        
        return true;
    }
}
