using System.Net.Http.Json;
using System;

using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace Messenger.Console.Client;

public class Program
{
    private static readonly string BaseUrl = "http://localhost:748/api";
    private static string? _token;
    private static int? _currentUserId;

    public static async Task Main(string[] args)
    {
    System.    Console.WriteLine("=== Messenger Console Client ===");
        System.Console.WriteLine();

        using var httpClient = new HttpClient();

        while (true)
        {
            if (string.IsNullOrEmpty(_token))
            {
                ShowMainMenu();
                var choice = System.Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await Register(httpClient);
                        break;
                    case "2":
                        await Login(httpClient);
                        break;
                    case "3":
                        System.Console.WriteLine("Выход из приложения...");
                        return;
                    default:
                        System.Console.WriteLine("Неверный выбор. Попробуйте снова.");
                        break;
                }
            }
            else
            {
                ShowLoggedInMenu();
                var choice = System.Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await CheckStatus(httpClient);
                        break;
                    case "2":
                        await ShowUsers(httpClient);
                        break;
                    case "3":
                        await SendMessage(httpClient);
                        break;
                    case "4":
                        await GetMessages(httpClient);
                        break;
                    case "5":
                        await Logout(httpClient);
                        break;
                    case "6":
                        System.Console.WriteLine("Выход из приложения...");
                        return;
                    default:
                        System.Console.WriteLine("Неверный выбор. Попробуйте снова.");
                        break;
                }
            }

            System.Console.WriteLine();
            System.Console.WriteLine("Нажмите Enter для продолжения...");
            System.Console.ReadLine();
            System.Console.Clear();
        }
    }

    private static void ShowMainMenu()
    {
        System.Console.WriteLine("Главное меню:");
        System.Console.WriteLine("1. Регистрация");
        System.Console.WriteLine("2. Вход");
        System.Console.WriteLine("3. Выход");
        System.Console.Write("Выберите действие: ");
    }

    private static void ShowLoggedInMenu()
    {
        System.Console.WriteLine("Меню (авторизован):");
        System.Console.WriteLine("1. Проверить статус аккаунта");
        System.Console.WriteLine("2. Показать список пользователей");
        System.Console.WriteLine("3. Отправить сообщение");
        System.Console.WriteLine("4. Получить сообщения");
        System.Console.WriteLine("5. Выйти из аккаунта");
        System.Console.WriteLine("6. Выход из приложения");
        System.Console.Write("Выберите действие: ");
    }

    private static async Task Register(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Регистрация ===");

        System.Console.Write("Введите имя пользователя: ");
        var username = System.Console.ReadLine();

        System.Console.Write("Введите пароль: ");
        var password = ReadPassword();
        System.Console.WriteLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            System.Console.WriteLine("Имя пользователя и пароль не могут быть пустыми.");
            return;
        }

        var request = new { username, password };
        
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{BaseUrl}/auth/register", request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                System.Console.WriteLine("✓ Регистрация успешна!");
                System.Console.WriteLine("Ожидайте одобрения администратора.");
                System.Console.WriteLine("После одобрения вы сможете войти в систему.");
            }
            else
            {
                var error = ParseErrorMessage(content);
                System.Console.WriteLine($"✗ Ошибка: {error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Ошибка подключения: {ex.Message}");
            System.Console.WriteLine("Убедитесь, что сервер запущен на http://localhost:748");
        }
    }

    private static async Task Login(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Вход ===");

        System.Console.Write("Введите имя пользователя: ");
        var username = System.Console.ReadLine();

        System.Console.Write("Введите пароль: ");
        var password = ReadPassword();
        System.Console.WriteLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            System.Console.WriteLine("Имя пользователя и пароль не могут быть пустыми.");
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

                System.Console.WriteLine("✓ Вход успешен!");
                System.Console.WriteLine($"Пользователь: {userUsername}");
                System.Console.WriteLine($"Роль: {role}");
                System.Console.WriteLine($"Статус: {status}");
                System.Console.WriteLine($"ID: {userId}");

                // Сохраняем токен для последующих запросов
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            }
            else
            {
                var error = ParseErrorMessage(content);
                System.Console.WriteLine($"✗ Ошибка входа: {error}");
                
                if (error.Contains("ожидает") || error.Contains("Pending"))
                {
                    System.Console.WriteLine("Ваш аккаунт ещё не одобрен администратором.");
                    System.Console.WriteLine("Админ-панель: http://localhost:228 (admin/admin123)");
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Ошибка подключения: {ex.Message}");
            System.Console.WriteLine("Убедитесь, что сервер запущен на http://localhost:748");
        }
    }

    private static async Task CheckStatus(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Статус аккаунта ===");
        
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

                System.Console.WriteLine($"ID: {userIdClaim?.Value ?? "N/A"}");
                System.Console.WriteLine($"Имя: {usernameClaim?.Value ?? "N/A"}");
                System.Console.WriteLine($"Роль: {roleClaim?.Value ?? "N/A"}");
                System.Console.WriteLine($"Токен действителен до: {jwtToken.ValidTo.ToLocalTime()}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Ошибка: {ex.Message}");
        }
    }

    private static async Task Logout(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Выход ===");
        
        try
        {
            var response = await httpClient.PostAsync($"{BaseUrl}/auth/logout", null);
            
            if (response.IsSuccessStatusCode)
            {
                System.Console.WriteLine("✓ Вы успешно вышли из системы.");
            }
            else
            {
                System.Console.WriteLine("⚠ Предупреждение при выходе (игнорируется).");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"⚠ Ошибка при выходе: {ex.Message}");
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
            var key = System.Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                System.Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                System.Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                System.Console.Write("*");
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

    private static async Task ShowUsers(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Список пользователей ===");
        
        try
        {
            var response = await httpClient.GetAsync($"{BaseUrl}/admin/users");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var users = JsonDocument.Parse(content).RootElement;
                
                System.Console.WriteLine($"{"ID",-5} {"Логин",-20} {"Статус",-10} {"Роль",-10}");
                System.Console.WriteLine(new string('-', 50));
                
                foreach (var user in users.EnumerateArray())
                {
                    var id = user.GetProperty("id").GetInt32();
                    var username = user.GetProperty("username").GetString();
                    var status = user.GetProperty("status").GetString();
                    var role = user.GetProperty("role").GetString();
                    
                    System.Console.WriteLine($"{id,-5} {username,-20} {status,-10} {role,-10}");
                }
            }
            else
            {
                System.Console.WriteLine("✗ Ошибка получения списка пользователей.");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Ошибка: {ex.Message}");
        }
    }

    private static async Task SendMessage(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Отправка сообщения ===");
        
        try
        {
            // Запрашиваем ID получателя
            System.Console.Write("Введите ID получателя (оставьте пустым для общего чата): ");
            var recipientIdInput = System.Console.ReadLine();
            
            int? recipientId = null;
            if (!string.IsNullOrEmpty(recipientIdInput) && int.TryParse(recipientIdInput, out var id))
            {
                recipientId = id;
            }
            
            // Если ID не указан, запрашиваем логин
            if (!recipientId.HasValue)
            {
                System.Console.Write("Введите логин получателя (оставьте пустым для общего чата): ");
                var recipientUsername = System.Console.ReadLine();
                
                if (!string.IsNullOrEmpty(recipientUsername))
                {
                    // Ищем пользователя по логину
                    var response = await httpClient.GetAsync($"{BaseUrl}/admin/users");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var users = JsonDocument.Parse(content).RootElement;
                        
                        foreach (var user in users.EnumerateArray())
                        {
                            var username = user.GetProperty("username").GetString();
                            if (username == recipientUsername)
                            {
                                recipientId = user.GetProperty("id").GetInt32();
                                System.Console.WriteLine($"Найден пользователь: {username} (ID: {recipientId})");
                                break;
                            }
                        }
                        
                        if (!recipientId.HasValue)
                        {
                            System.Console.WriteLine($"✗ Пользователь с логином '{recipientUsername}' не найден.");
                            return;
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("✗ Ошибка получения списка пользователей.");
                        return;
                    }
                }
            }
            
            // Ввод текста сообщения
            System.Console.Write("Введите текст сообщения: ");
            var text = System.Console.ReadLine();
            
            if (string.IsNullOrEmpty(text))
            {
                System.Console.WriteLine("✗ Сообщение не может быть пустым.");
                return;
            }
            
            // Формируем запрос
            var request = new
            {
                text,
                recipientId
            };
            
            var sendResponse = await httpClient.PostAsJsonAsync($"{BaseUrl}/chat/messages", request);
            var sendContent = await sendResponse.Content.ReadAsStringAsync();
            
            if (sendResponse.IsSuccessStatusCode)
            {
                if (recipientId.HasValue)
                {
                    System.Console.WriteLine("✓ Личное сообщение отправлено!");
                }
                else
                {
                    System.Console.WriteLine("✓ Сообщение отправлено в общий чат!");
                }
            }
            else
            {
                var error = ParseErrorMessage(sendContent);
                System.Console.WriteLine($"✗ Ошибка отправки: {error}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Ошибка: {ex.Message}");
        }
    }

    private static async Task GetMessages(HttpClient httpClient)
    {
        System.Console.WriteLine("\n=== Сообщения ===");
        
        try
        {
            var response = await httpClient.GetAsync($"{BaseUrl}/chat/messages");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var messagesDoc = JsonDocument.Parse(content);
                var messagesArray = messagesDoc.RootElement.EnumerateArray().ToList();
                
                if (messagesArray.Count == 0)
                {
                    System.Console.WriteLine("Сообщений нет.");
                    return;
                }
                
                foreach (var msg in messagesArray)
                {
                    var id = msg.GetProperty("id").GetInt32();
                    var senderId = msg.GetProperty("senderId").GetInt32();
                    var senderName = msg.GetProperty("senderName").GetString();
                    var text = msg.GetProperty("text").GetString();
                    var createdAt = msg.GetProperty("createdAt").GetDateTime().ToLocalTime();
                    
                    // Проверяем, личное ли это сообщение
                    bool isPersonal = msg.TryGetProperty("recipientId", out var recipientEl) && 
                                     recipientEl.ValueKind != JsonValueKind.Null;
                    
                    string prefix = isPersonal ? "[ЛИЧНОЕ]" : "[ОБЩЕЕ]";
                    System.Console.WriteLine($"{prefix} [{createdAt:HH:mm}] {senderName}: {text}");
                }
            }
            else
            {
                System.Console.WriteLine("✗ Ошибка получения сообщений.");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Ошибка: {ex.Message}");
        }
    }
}
