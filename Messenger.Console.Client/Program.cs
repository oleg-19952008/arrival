using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace Messenger.Console.Client;

public class Program
{
    private static readonly string BaseUrl = "http://localhost:748/api";
    private static string? _token;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Messenger Console Client ===");
        Console.WriteLine();

        using var httpClient = new HttpClient();

        while (true)
        {
            if (string.IsNullOrEmpty(_token))
            {
                ShowMainMenu();
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await Register(httpClient);
                        break;
                    case "2":
                        await Login(httpClient);
                        break;
                    case "3":
                        Console.WriteLine("Выход из приложения...");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор. Попробуйте снова.");
                        break;
                }
            }
            else
            {
                ShowLoggedInMenu();
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await CheckStatus(httpClient);
                        break;
                    case "2":
                        await Logout(httpClient);
                        break;
                    case "3":
                        Console.WriteLine("Выход из приложения...");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор. Попробуйте снова.");
                        break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Нажмите Enter для продолжения...");
            Console.ReadLine();
            Console.Clear();
        }
    }

    private static void ShowMainMenu()
    {
        Console.WriteLine("Главное меню:");
        Console.WriteLine("1. Регистрация");
        Console.WriteLine("2. Вход");
        Console.WriteLine("3. Выход");
        Console.Write("Выберите действие: ");
    }

    private static void ShowLoggedInMenu()
    {
        Console.WriteLine("Меню (авторизован):");
        Console.WriteLine("1. Проверить статус аккаунта");
        Console.WriteLine("2. Выйти из аккаунта");
        Console.WriteLine("3. Выход из приложения");
        Console.Write("Выберите действие: ");
    }

    private static async Task Register(HttpClient httpClient)
    {
        Console.WriteLine("\n=== Регистрация ===");
        
        Console.Write("Введите имя пользователя: ");
        var username = Console.ReadLine();
        
        Console.Write("Введите пароль: ");
        var password = ReadPassword();
        Console.WriteLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Имя пользователя и пароль не могут быть пустыми.");
            return;
        }

        var request = new { username, password };
        
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{BaseUrl}/auth/register", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ Регистрация успешна!");
                Console.WriteLine("Ожидайте одобрения администратора.");
                Console.WriteLine("После одобрения вы сможете войти в систему.");
            }
            else
            {
                var error = ParseErrorMessage(content);
                Console.WriteLine($"✗ Ошибка: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Ошибка подключения: {ex.Message}");
            Console.WriteLine("Убедитесь, что сервер запущен на http://localhost:748");
        }
    }

    private static async Task Login(HttpClient httpClient)
    {
        Console.WriteLine("\n=== Вход ===");
        
        Console.Write("Введите имя пользователя: ");
        var username = Console.ReadLine();
        
        Console.Write("Введите пароль: ");
        var password = ReadPassword();
        Console.WriteLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Имя пользователя и пароль не могут быть пустыми.");
            return;
        }

        var request = new { username, password };
        
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{BaseUrl}/auth/login", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JsonDocument.Parse(content);
                _token = data.RootElement.GetProperty("token").GetString();
                
                var user = data.RootElement.GetProperty("user");
                var userId = user.GetProperty("id").GetInt32();
                var userUsername = user.GetProperty("username").GetString();
                var role = user.GetProperty("role").GetString();
                var status = user.GetProperty("status").GetString();

                Console.WriteLine("✓ Вход успешен!");
                Console.WriteLine($"Пользователь: {userUsername}");
                Console.WriteLine($"Роль: {role}");
                Console.WriteLine($"Статус: {status}");
                Console.WriteLine($"ID: {userId}");

                // Сохраняем токен для последующих запросов
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }
            else
            {
                var error = ParseErrorMessage(content);
                Console.WriteLine($"✗ Ошибка входа: {error}");
                
                if (error.Contains("ожидает") || error.Contains("Pending"))
                {
                    Console.WriteLine("Ваш аккаунт ещё не одобрен администратором.");
                    Console.WriteLine("Админ-панель: http://localhost:228 (admin/admin123)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Ошибка подключения: {ex.Message}");
            Console.WriteLine("Убедитесь, что сервер запущен на http://localhost:748");
        }
    }

    private static async Task CheckStatus(HttpClient httpClient)
    {
        Console.WriteLine("\n=== Статус аккаунта ===");
        
        try
        {
            // Декодируем токен для получения информации
            if (!string.IsNullOrEmpty(_token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(_token);
                
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "userId");
                var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "username");
                var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "role");

                Console.WriteLine($"ID: {userIdClaim?.Value ?? "N/A"}");
                Console.WriteLine($"Имя: {usernameClaim?.Value ?? "N/A"}");
                Console.WriteLine($"Роль: {roleClaim?.Value ?? "N/A"}");
                Console.WriteLine($"Токен действителен до: {jwtToken.ValidTo.ToLocalTime()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Ошибка: {ex.Message}");
        }
    }

    private static async Task Logout(HttpClient httpClient)
    {
        Console.WriteLine("\n=== Выход ===");
        
        try
        {
            var response = await httpClient.PostAsync($"{BaseUrl}/auth/logout", null);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ Вы успешно вышли из системы.");
            }
            else
            {
                Console.WriteLine("⚠ Предупреждение при выходе (игнорируется).");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Ошибка при выходе: {ex.Message}");
        }
        finally
        {
            _token = null;
            httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static string ReadPassword()
    {
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }
        return password.ToString();
    }

    private static string ParseErrorMessage(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var messageProp))
            {
                return messageProp.GetString() ?? "Неизвестная ошибка";
            }
        }
        catch
        {
            // Игнорируем ошибки парсинга
        }
        return "Неизвестная ошибка";
    }
}
