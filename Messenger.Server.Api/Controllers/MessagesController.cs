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

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// Получить все сообщения
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var messages = await _messageService.GetAllMessagesAsync();
        return Ok(messages.Select(m => new 
        {
            id = m.Id,
            senderId = m.SenderId,
            senderName = m.SenderName,
            type = m.Type.ToString(),
            content = m.Content,
            createdAt = m.CreatedAt,
            isDeleted = m.IsDeleted
        }));
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
        
        var messageType = request.Type.ToLower() == "file" ? MessageType.File : MessageType.Text;
        var message = await _messageService.SendMessageAsync(userId.Value, messageType, request.Content);
        
        return Ok(new 
        {
            id = message.Id,
            senderId = message.SenderId,
            senderName = message.SenderName,
            type = message.Type.ToString(),
            content = message.Content,
            createdAt = message.CreatedAt
        });
    }

    /// <summary>
    /// Удалить сообщение
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _messageService.DeleteMessageAsync(id);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Сообщение удалено" });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("userId");
        if (claim != null && int.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text или file
}
