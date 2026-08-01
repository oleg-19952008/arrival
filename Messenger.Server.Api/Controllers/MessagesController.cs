using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Messenger.Core.Interfaces;
using Messenger.Core.Models;

namespace Messenger.Server.Api.Controllers;

/// <summary>
/// Контроллер сообщений
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IUserRepository _userRepository;

    public MessagesController(IMessageService messageService, IUserRepository userRepository)
    {
        _messageService = messageService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Получить все сообщения с поддержкой пагинации
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var messages = await _messageService.GetAllMessagesAsync();
        var paginatedMessages = messages.Skip(offset).Take(limit);
        
        return Ok(paginatedMessages.Select(m => new 
        {
            id = m.Id,
            senderId = m.SenderId,
            senderName = m.SenderName,
            recipientId = m.RecipientId,
            type = m.Type.ToString(),
            content = m.Content,
            createdAt = m.CreatedAt,
            isDeleted = m.IsDeleted
        }).OrderBy(m => m.createdAt)); // Сортировка по времени (новые в конце)
    }

    /// <summary>
    /// Получить сообщения для текущего пользователя (личные + общие)
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyMessages()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();
        
        var messages = await _messageService.GetMessagesForUserAsync(userId.Value);
        return Ok(messages.Select(m => new 
        {
            id = m.Id,
            senderId = m.SenderId,
            senderName = m.SenderName,
            recipientId = m.RecipientId,
            type = m.Type.ToString(),
            content = m.Content,
            createdAt = m.CreatedAt,
            isDeleted = m.IsDeleted
        }).OrderBy(m => m.createdAt)); // Сортировка по времени (новые в конце)
    }

    /// <summary>
    /// Отправить сообщение
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();
        
        int? recipientId = null;
        
        // Определяем получателя: по ID или по логину
        if (!string.IsNullOrEmpty(request.RecipientLogin))
        {
            var recipient = await _userRepository.GetByUsernameAsync(request.RecipientLogin);
            if (recipient == null)
                return BadRequest(new { message = $"Пользователь с логином '{request.RecipientLogin}' не найден" });
            recipientId = recipient.Id;
        }
        else if (request.RecipientId.HasValue)
        {
            recipientId = request.RecipientId;
        }
        
        var messageType = request.Type.ToLower() == "file" ? MessageType.File : MessageType.Text;
        var message = await _messageService.SendMessageAsync(userId.Value, messageType, request.Content, recipientId);
        
        return Ok(new 
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = message.SenderName,
            recipientId = message.RecipientId,
            type = message.Type.ToString(),
            content = message.Content,
            createdAt = message.CreatedAt
        });
    }

    /// <summary>
    /// Удалить сообщение (мягкое удаление с удалением файлов)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _messageService.DeleteMessageAsync(id);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Сообщение удалено" });
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var message = await _messageService.GetMessageByIdAsync(id);
        if (message == null)
            return NotFound(new { message = "Сообщение не найдено" });
        
        return Ok(new 
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = message.SenderName,
            recipientId = message.RecipientId,
            type = message.Type.ToString(),
            content = message.Content,
            createdAt = message.CreatedAt,
            isDeleted = message.IsDeleted
        });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim != null && int.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text или file
    public int? RecipientId { get; set; } // ID получателя (null для общего чата)
    public string? RecipientLogin { get; set; } // Логин получателя (альтернатива RecipientId)
}
