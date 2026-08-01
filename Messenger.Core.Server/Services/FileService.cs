using Messenger.Core.Models;
using Messenger.Core.Interfaces;
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
        }
    }
    
    public async Task<FileAttachment?> UploadFileAsync(Stream fileStream, string fileName, string contentType, int messageId)
    {
        // Проверка существования сообщения
        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null)
        {
            return null;
        }
        
        // Генерация уникального имени файла
        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(_uploadFolder, uniqueFileName);
        
        // Сохранение файла на диск
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await fileStream.CopyToAsync(fs);
        }
        
        // Получение размера файла
        var fileSize = new FileInfo(filePath).Length;
        
        // Создание записи в БД
        var attachment = new FileAttachment
        {
            MessageId = messageId,
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSize = fileSize,
            UploadedAt = DateTime.UtcNow
        };
        
        return await _fileRepository.AddAsync(attachment);
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
            return null;
        }
        
        var stream = new FileStream(
            attachment.FilePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.Read);
        
        return (stream, attachment.ContentType, attachment.FileName);
    }
    
    public async Task<bool> DeleteFileAsync(int fileId)
    {
        var attachment = await _fileRepository.GetByIdAsync(fileId);
        if (attachment == null)
        {
            return false;
        }
        
        // Удаление файла с диска
        if (File.Exists(attachment.FilePath))
        {
            File.Delete(attachment.FilePath);
        }
        
        // Удаление записи из БД
        await _fileRepository.DeleteAsync(fileId);
        
        return true;
    }
}
