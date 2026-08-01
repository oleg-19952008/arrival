using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Messenger.Core.Interfaces;
using Messenger.Core.Models;

namespace Messenger.Server.Api.Controllers;

/// <summary>
/// Контроллер управления файлами
/// </summary>
[ApiController]
[Route("api/upload")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly IMessageService _messageService;

    public FilesController(IFileService fileService, IMessageService messageService)
    {
        _fileService = fileService;
        _messageService = messageService;
    }

    /// <summary>
    /// Загрузить файл с валидацией размера и типа
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] int? messageId = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Файл не предоставлен" });

        // Валидация размера файла (10 MB)
        const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
        if (file.Length > MaxFileSize)
            return StatusCode(413, new { message = $"Размер файла превышает лимит {MaxFileSize / 1024 / 1024} MB" });

        // Валидация типа файла
        var allowedContentTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf",
            "text/plain",
            "application/zip",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        
        if (!allowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { message = $"Неподдерживаемый тип файла: {file.ContentType}" });

        int msgId;
        
        // Если messageId не указан, создаём новое сообщение
        if (!messageId.HasValue)
        {
            var message = await _messageService.SendMessageAsync(
                userId.Value, 
                MessageType.File, 
                $"Файл: {file.FileName}");
            msgId = message.Id;
        }
        else
        {
            msgId = messageId.Value;
        }

        // Загрузка файла
        using (var stream = file.OpenReadStream())
        {
            var attachment = await _fileService.UploadFileAsync(
                stream, 
                file.FileName, 
                file.ContentType, 
                msgId);

            if (attachment == null)
                return BadRequest(new { message = "Ошибка при загрузке файла" });

            return Ok(new
            {
                messageId = msgId,
                file = new
                {
                    id = attachment.Id,
                    fileId = attachment.FileId,  // Возвращаем UUID
                    fileName = attachment.FileName,
                    contentType = attachment.ContentType,
                    fileSize = attachment.FileSize,
                    uploadedAt = attachment.UploadedAt
                }
            });
        }
    }

    /// <summary>
    /// Скачать файл по FileId (UUID)
    /// </summary>
    [HttpGet("download/{fileId}")]
    public async Task<IActionResult> DownloadFile(string fileId)
    {
        var result = await _fileService.DownloadFileByFileIdAsync(fileId);
        
        if (result == null)
            return NotFound(new { message = "Файл не найден" });

        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Удалить файл
    /// </summary>
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteFile(int id)
    {
        var success = await _fileService.DeleteFileAsync(id);
        
        if (!success)
            return NotFound(new { message = "Файл не найден" });

        return Ok(new { message = "Файл удалён" });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim != null && int.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }
}
