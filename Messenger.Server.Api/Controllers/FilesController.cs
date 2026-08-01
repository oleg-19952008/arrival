using Microsoft.AspNetCore.Mvc;
using Messenger.Core.Interfaces;
using Messenger.Core.Models;

namespace Messenger.Server.Api.Controllers;

/// <summary>
/// Контроллер управления файлами
/// </summary>
[ApiController]
[Route("api/[controller]")]
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
    /// Загрузить файл
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] int messageId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Файл не предоставлен" });

        // Создание сообщения с типом File
        var message = await _messageService.SendMessageAsync(
            userId.Value, 
            MessageType.File, 
            $"Файл: {file.FileName}");

        // Загрузка файла
        using (var stream = file.OpenReadStream())
        {
            var attachment = await _fileService.UploadFileAsync(
                stream, 
                file.FileName, 
                file.ContentType, 
                message.Id);

            if (attachment == null)
                return BadRequest(new { message = "Ошибка при загрузке файла" });

            return Ok(new
            {
                messageId = message.Id,
                file = new
                {
                    id = attachment.Id,
                    fileName = attachment.FileName,
                    contentType = attachment.ContentType,
                    fileSize = attachment.FileSize,
                    uploadedAt = attachment.UploadedAt
                }
            });
        }
    }

    /// <summary>
    /// Скачать файл по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> DownloadFile(int id)
    {
        var result = await _fileService.DownloadFileAsync(id);
        
        if (result == null)
            return NotFound(new { message = "Файл не найден" });

        return File(result.Value.FileStream, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Удалить файл
    /// </summary>
    [HttpDelete("{id}")]
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
