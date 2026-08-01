using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace Messenger.Admin.Web.Services;

/// <summary>
/// Сервис для работы с API сервера
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:123");
    }

    public void SetToken(string token)
    {
        _token = token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        var content = new StringContent(
            JsonConvert.SerializeObject(new { username, password }),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync("/api/auth/login", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var result = JsonConvert.DeserializeObject<LoginResponse>(responseBody);
            return new LoginResult 
            { 
                Success = true, 
                Token = result?.Token,
                User = result?.User
            };
        }

        return new LoginResult 
        { 
            Success = false, 
            ErrorMessage = responseBody 
        };
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var response = await _httpClient.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();
        
        var responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<IEnumerable<UserDto>>(responseBody) ?? Enumerable.Empty<UserDto>();
    }

    public async Task<bool> ApproveUserAsync(int userId)
    {
        var response = await _httpClient.PostAsync($"/api/users/{userId}/approve", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BlockUserAsync(int userId)
    {
        var response = await _httpClient.PostAsync($"/api/users/{userId}/block", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var response = await _httpClient.DeleteAsync($"/api/users/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<IEnumerable<MessageDto>> GetAllMessagesAsync()
    {
        var response = await _httpClient.GetAsync("/api/messages");
        response.EnsureSuccessStatusCode();
        
        var responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<IEnumerable<MessageDto>>(responseBody) ?? Enumerable.Empty<MessageDto>();
    }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserInfo? User { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LoginResponse
{
    public string? Token { get; set; }
    public UserInfo? User { get; set; }
}

public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class MessageDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string? SenderName { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
