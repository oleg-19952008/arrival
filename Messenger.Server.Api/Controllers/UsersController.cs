using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Messenger.Core.Interfaces;
using Messenger.Core.Models;

namespace Messenger.Server.Api.Controllers;

/// <summary>
/// Контроллер пользователей (только для администраторов)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Получить всех пользователей
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users.Select(u => new 
        {
            id = u.Id,
            username = u.Username,
            role = u.Role.ToString(),
            status = u.Status.ToString(),
            createdAt = u.CreatedAt,
            lastLoginAt = u.LastLoginAt
        }));
    }

    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();
        
        return Ok(new 
        {
            id = user.Id,
            username = user.Username,
            role = user.Role.ToString(),
            status = user.Status.ToString(),
            createdAt = user.CreatedAt,
            lastLoginAt = user.LastLoginAt
        });
    }

    /// <summary>
    /// Одобрить пользователя (изменить статус на Active)
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveUser(int id)
    {
        var result = await _userService.UpdateUserStatusAsync(id, UserStatus.Active);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь одобрен" });
    }

    /// <summary>
    /// Заблокировать пользователя
    /// </summary>
    [HttpPost("{id}/block")]
    public async Task<IActionResult> BlockUser(int id)
    {
        var result = await _userService.UpdateUserStatusAsync(id, UserStatus.Blocked);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь заблокирован" });
    }

    /// <summary>
    /// Удалить пользователя
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь удалён" });
    }
}
