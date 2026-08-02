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
    /// Получить пользователя по имени (доступно всем авторизованным)
    /// </summary>
    [HttpGet("by-username/{username}")]
    [Authorize]
    public async Task<IActionResult> GetByUsername(string username)
    {
        var userRepository = HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByUsernameAsync(username);
        if (user == null)
            return NotFound();

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            role = user.Role.ToString(),
            status = user.Status.ToString()
        });
    }

    /// <summary>
    /// Получить всех пользователей (доступно всем авторизованным для выбора получателя)
    /// </summary>
    [HttpGet("list")]
    [Authorize]
    public async Task<IActionResult> GetUsersList()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users.Where(u => u.Status == UserStatus.Active).Select(u => new 
        {
            id = u.Id,
            username = u.Username
        }));
    }

    /// <summary>
    /// Получить всех пользователей с фильтром по статусу (доступно всем авторизованным)
    /// </summary>
    [HttpGet("all")]
    [Authorize]
    public async Task<IActionResult> GetAllWithFilter([FromQuery] string? status = null)
    {
        var users = await _userService.GetAllUsersAsync();
        
        var query = users.AsQueryable();
        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<UserStatus>(status, ignoreCase: true, out var userStatus))
            {
                query = query.Where(u => u.Status == userStatus);
            }
        }
        
        return Ok(query.Select(u => new 
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
    /// Заблокировать пользователя (ban)
    /// </summary>
    [HttpPost("{id}/ban")]
    public async Task<IActionResult> BanUser(int id)
    {
        var result = await _userService.UpdateUserStatusAsync(id, UserStatus.Banned);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь заблокирован" });
    }

    /// <summary>
    /// Заблокировать пользователя (block - алиас для ban)
    /// </summary>
    [HttpPost("{id}/block")]
    public async Task<IActionResult> BlockUser(int id)
    {
        var result = await _userService.UpdateUserStatusAsync(id, UserStatus.Banned);
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
    
    /// <summary>
    /// Разблокировать пользователя
    /// </summary>
    [HttpPost("{id}/unban")]
    public async Task<IActionResult> UnbanUser(int id)
    {
        var result = await _userService.UnbanUserAsync(id);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь разблокирован" });
    }
    
    /// <summary>
    /// Разблокировать пользователя (unblock - алиас для unban)
    /// </summary>
    [HttpPost("{id}/unblock")]
    public async Task<IActionResult> UnblockUser(int id)
    {
        var result = await _userService.UnbanUserAsync(id);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь разблокирован" });
    }
    
    /// <summary>
    /// Сменить пароль пользователя
    /// </summary>
    [HttpPost("{id}/password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.NewPassword))
            return BadRequest(new { message = "Пароль не может быть пустым" });
        
        var result = await _userService.ChangePasswordAsync(id, request.NewPassword);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пароль изменён" });
    }
}

public class ChangePasswordRequest
{
    public string NewPassword { get; set; }
}
