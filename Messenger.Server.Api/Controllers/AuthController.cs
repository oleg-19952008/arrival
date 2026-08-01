using Microsoft.AspNetCore.Mvc;
using Messenger.Core.Interfaces;
using Messenger.Core.Models;

namespace Messenger.Server.Api.Controllers;

/// <summary>
/// Контроллер аутентификации
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Регистрация нового пользователя
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request.Username, request.Password);
        
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });
        
        return Ok(new { message = "Пользователь зарегистрирован. Ожидайте одобрения администратора." });
    }

    /// <summary>
    /// Вход в систему
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password);
        
        if (!result.Success)
            return Unauthorized(new { message = result.ErrorMessage });
        
        return Ok(new 
        { 
            token = result.Token,
            user = new 
            {
                id = result.User!.Id,
                username = result.User.Username,
                role = result.User.Role.ToString(),
                status = result.User.Status.ToString()
            }
        });
    }

    /// <summary>
    /// Выход из системы
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();
        
        await _authService.LogoutAsync(userId.Value);
        return Ok(new { message = "Выход выполнен" });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("userId");
        if (claim != null && int.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
