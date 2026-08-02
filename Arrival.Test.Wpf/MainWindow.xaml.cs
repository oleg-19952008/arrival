using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace Arrival.Test.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly string _baseUrl = "http://localhost:748/api";
        private readonly string _signalRUrl = "http://localhost:748/notificationHub";
        
        private readonly HttpClient _httpClient;
        private string? _token;
        private int? _currentUserId;
        private string? _currentUsername;
        private HubConnection? _hubConnection;
        private bool _isConnectedToHub;
        private DispatcherTimer? _messagesPollingTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            // Увеличиваем буфер для больших запросов
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
            
            MainTabControl.SelectedIndex = 0;
            
            // Инициализация таймера для опроса сообщений каждые 5 секунд
            _messagesPollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _messagesPollingTimer.Tick += async (s, e) => await LoadMessages();
        }

        #region Auth Methods

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformRegistration();
        }

        private async Task PerformRegistration()
        {
            var username = RegisterUsernameTextBox.Text.Trim();
            var password = RegisterPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                AuthMessageTextBlock.Text = "Имя пользователя и пароль не могут быть пустыми.";
                return;
            }

            try
            {
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/register", request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    AuthMessageTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    AuthMessageTextBlock.Text = "✓ Регистрация успешна! Ожидайте одобрения администратора.";
                    RegisterUsernameTextBox.Clear();
                    RegisterPasswordBox.Clear();
                }
                else
                {
                    var error = ParseErrorMessage(content);
                    AuthMessageTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    AuthMessageTextBlock.Text = $"✗ Ошибка: {error}";
                }
            }
            catch (Exception ex)
            {
                AuthMessageTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                AuthMessageTextBlock.Text = $"✗ Ошибка подключения: {ex.Message}\nУбедитесь, что сервер запущен на http://localhost:748";
            }
        }

        private void LoginShowButton_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = LoginTabItem;
        }

        private void BackToRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = AuthTabItem;
        }

        #region KeyDown Handlers for Enter key

        private void RegisterUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                RegisterPasswordBox.Focus();
            }
        }

        private void RegisterPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _ = PerformRegistration();
            }
        }

        private void LoginUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                LoginPasswordBox.Focus();
            }
        }

        private void LoginPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _ = PerformLogin();
            }
        }

        #endregion

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformLogin();
        }

        private async Task PerformLogin()
        {
            var username = LoginUsernameTextBox.Text.Trim();
            var password = LoginPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                LoginMessageTextBlock.Text = "Имя пользователя и пароль не могут быть пустыми.";
                return;
            }

            try
            {
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/login", request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonDocument.Parse(content);
                    _token = data.RootElement.GetProperty("token").GetString();
                    
                    var user = data.RootElement.GetProperty("user");
                    _currentUserId = user.GetProperty("id").GetInt32();
                    _currentUsername = user.GetProperty("username").GetString();
                    var role = user.GetProperty("role").GetString();
                    var status = user.GetProperty("status").GetString();

                    // Сохраняем токен для последующих запросов
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", _token);

                    // Обновляем UI
                    UpdateLoggedInUI(_currentUsername, role, status);
                    
                    // Автоматически подключаемся к WebSocket
                    await ConnectToWebSocket();

                    LoginMessageTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    LoginMessageTextBlock.Text = "✓ Вход успешен!";
                    
                    LoginUsernameTextBox.Clear();
                    LoginPasswordBox.Clear();
                }
                else
                {
                    var error = ParseErrorMessage(content);
                    LoginMessageTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    LoginMessageTextBlock.Text = $"✗ Ошибка входа: {error}";
                    
                    if (error.Contains("ожидает") || error.Contains("Pending"))
                    {
                        LoginMessageTextBlock.Text += "\nВаш аккаунт ещё не одобрен администратором.\nАдмин-панель: http://localhost:228 (admin/admin123)";
                    }
                }
            }
            catch (Exception ex)
            {
                LoginMessageTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                LoginMessageTextBlock.Text = $"✗ Ошибка подключения: {ex.Message}";
            }
        }

        private void UpdateLoggedInUI(string username, string role, string status)
        {
            StatusTextBlock.Text = "Авторизован";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
            UserInfoTextBlock.Text = $"{username} | Роль: {role} | Статус: {status}";

            // Включаем вкладки
            MessagesTabItem.IsEnabled = true;
            UsersTabItem.IsEnabled = true;
            WebSocketTabItem.IsEnabled = true;

            // Показываем кнопку выхода
            LogoutButton.Visibility = Visibility.Visible;
            
            // Запускаем таймер опроса сообщений
            _messagesPollingTimer?.Start();
            
            // Загружаем сообщения сразу после входа
            _ = LoadMessages();
        }

        private void ResetUI()
        {
            StatusTextBlock.Text = "Не авторизован";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Black;
            UserInfoTextBlock.Text = "";

            // Отключаем вкладки
            MessagesTabItem.IsEnabled = false;
            UsersTabItem.IsEnabled = false;
            WebSocketTabItem.IsEnabled = false;

            // Скрываем кнопку выхода
            LogoutButton.Visibility = Visibility.Collapsed;

            // Переключаемся на вкладку авторизации
            MainTabControl.SelectedItem = AuthTabItem;
            
            // Останавливаем таймер опроса сообщений
            _messagesPollingTimer?.Stop();
            
            // Очищаем поле сообщений
            MessagesTextBox.Clear();
        }

        #endregion

        #region Logout & Exit

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectFromWebSocket();

            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/auth/logout", null);
                
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Вы успешно вышли из системы.", "Выход", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выходе: {ex.Message}", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _token = null;
                _currentUserId = null;
                _currentUsername = null;
                _httpClient.DefaultRequestHeaders.Authorization = null;
                ResetUI();
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region Messages

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
            
            // Сразу обновляем сообщения после отправки
            await LoadMessages();
        }

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var recipientInput = RecipientTextBox.Text.Trim();
            var content = MessageTextBox.Text.Trim();

            if (string.IsNullOrEmpty(content))
            {
                MessageBox.Show("Сообщение не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int? recipientId = null;
                string? recipientLogin = null;

                if (!string.IsNullOrEmpty(recipientInput))
                {
                    // Проверяем, является ли ввод числом (ID)
                    if (int.TryParse(recipientInput, out int parsedId))
                    {
                        recipientId = parsedId;
                    }
                    else
                    {
                        // Пытаемся найти пользователя по логину
                        var response = await _httpClient.GetAsync($"{_baseUrl}/users");
                        if (response.IsSuccessStatusCode)
                        {
                            var readContent = await response.Content.ReadAsStringAsync();
                            var users = JsonDocument.Parse(readContent).RootElement;

                            foreach (var user in users.EnumerateArray())
                            {
                                var username = user.GetProperty("username").GetString();
                                if (username == recipientInput)
                                {
                                    recipientId = user.GetProperty("id").GetInt32();
                                    break;
                                }
                            }

                            if (!recipientId.HasValue)
                            {
                                MessageBox.Show($"Пользователь с логином '{recipientInput}' не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show("Ошибка получения списка пользователей.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                var request = new
                {
                    content,
                    type = "text",
                    recipientId,
                    recipientLogin
                };

                // Используем StringContent с явным указанием UTF-8 для корректной отправки больших текстов
                var jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
                var stringContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                var sendResponse = await _httpClient.PostAsync($"{_baseUrl}/messages", stringContent);
                var sendContent = await sendResponse.Content.ReadAsStringAsync();

                if (sendResponse.IsSuccessStatusCode)
                {
                    MessageTextBox.Clear();

                    // Сразу обновляем список сообщений после отправки
                    await LoadMessages();
                }
                else
                {
                    var error = ParseErrorMessage(sendContent);
                    MessageBox.Show($"✗ Ошибка отправки: {error}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"✗ Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void RefreshMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadMessages();
        }

        private async Task LoadMessages()
        {
            try
            {
                MessagesStatusTextBlock.Text = "Загрузка...";
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/messages/my");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messagesDoc = JsonDocument.Parse(content);
                    var messagesArray = messagesDoc.RootElement.EnumerateArray().ToList();

                    MessagesTextBox.Clear();

                    if (messagesArray.Count == 0)
                    {
                        MessagesTextBox.Text = "Сообщений нет.";
                        MessagesStatusTextBlock.Text = "";
                        return;
                    }

                    var messagesText = new StringBuilder();
                    
                    foreach (var msg in messagesArray)
                    {
                        var id = msg.GetProperty("id").GetInt32();
                        var senderId = msg.GetProperty("senderId").GetInt32();
                        var senderName = msg.GetProperty("senderName").GetString();
                        var textContent = msg.GetProperty("content").GetString();
                        var createdAt = msg.GetProperty("createdAt").GetDateTime().ToLocalTime();

                        bool isPersonal = msg.TryGetProperty("recipientId", out var recipientEl) && 
                                         recipientEl.ValueKind != JsonValueKind.Null;

                        string prefix = isPersonal ? "[ЛИЧНОЕ]" : "[ОБЩЕЕ]";
                        string displayName = (senderId == _currentUserId) ? "Вы" : senderName;
                        
                        messagesText.AppendLine($"{prefix} [{createdAt:dd.MM.yyyy HH:mm:ss}] {displayName}:");
                        messagesText.AppendLine(textContent);
                        messagesText.AppendLine(new string('-', 80));
                    }

                    MessagesTextBox.Text = messagesText.ToString().TrimEnd();
                    
                    // Прокрутка вниз к последнему сообщению
                    MessagesTextBox.CaretIndex = MessagesTextBox.Text.Length;
                    MessagesTextBox.ScrollToEnd();
                    
                    MessagesStatusTextBlock.Text = $"Загружено сообщений: {messagesArray.Count}";
                }
                else
                {
                    MessagesStatusTextBlock.Text = "Ошибка получения сообщений.";
                }
            }
            catch (Exception ex)
            {
                MessagesStatusTextBlock.Text = $"Ошибка: {ex.Message}";
            }
        }

        #endregion

        #region Users

        private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsers();
        }

        private async Task LoadUsers(bool showError = true)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/users");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var users = JsonDocument.Parse(content).RootElement;

                    UsersListBox.Items.Clear();

                    foreach (var user in users.EnumerateArray())
                    {
                        var id = user.GetProperty("id").GetInt32();
                        var username = user.GetProperty("username").GetString();
                        var status = user.GetProperty("status").GetString();
                        var role = user.GetProperty("role").GetString();

                        UsersListBox.Items.Add(new
                        {
                            Id = id,
                            Username = username,
                            Status = status,
                            Role = role
                        });
                    }
                }
                else if (showError)
                {
                    MessageBox.Show("Ошибка получения списка пользователей.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                if (showError)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #endregion

        #region WebSocket

        private async void ConnectWebSocketButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToWebSocket();
        }

        private async void DisconnectWebSocketButton_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectFromWebSocket();
        }

        private async Task ConnectToWebSocket()
        {
            if (_isConnectedToHub)
            {
                WebSocketStatusTextBlock.Text = "Статус: Уже подключено";
                return;
            }

            if (_currentUserId == null)
            {
                MessageBox.Show("Сначала войдите в систему.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                WebSocketStatusTextBlock.Text = "Статус: Подключение...";
                
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{_signalRUrl}?userId={_currentUserId}&token={_token}")
                    .WithAutomaticReconnect()
                    .Build();

                // Обработчик входящих сообщений
                _hubConnection.On<int, string, int, string, DateTime>("MessageReceived", 
                    (messageId, senderName, senderId, content, receivedAt) =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        RealTimeMessagesListBox.Items.Add($"[{receivedAt.ToLocalTime():HH:mm}] {senderName}: {content}");
                        
                        // Автоматически обновляем список сообщений
                        await LoadMessages();
                        
                        // Показываем уведомление
                        MessageBox.Show(
                            $"Новое сообщение от {senderName}:\n{content}",
                            "Новое сообщение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                });

                // Обработчик уведомлений
                _hubConnection.On<string, string>("NotificationReceived", 
                    (type, data) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        RealTimeMessagesListBox.Items.Add($"[УВЕДОМЛЕНИЕ] {type}: {data}");
                    });
                });

                await _hubConnection.StartAsync();
                _isConnectedToHub = true;
                
                WebSocketStatusTextBlock.Text = "Статус: Подключено";
                WebSocketInfoTextBlock.Text = $"Пользователь ID: {_currentUserId}";
                
                MessageBox.Show("✓ Успешно подключено к WebSocket для real-time сообщений!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WebSocketStatusTextBlock.Text = "Статус: Ошибка подключения";
                WebSocketInfoTextBlock.Text = ex.Message;
                _isConnectedToHub = false;
                
                MessageBox.Show($"✗ Ошибка подключения к WebSocket: {ex.Message}\nПродолжение работы без real-time уведомлений.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task DisconnectFromWebSocket()
        {
            if (!_isConnectedToHub || _hubConnection == null)
            {
                return;
            }

            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                _isConnectedToHub = false;
                
                WebSocketStatusTextBlock.Text = "Статус: Отключено";
                WebSocketInfoTextBlock.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"✗ Ошибка отключения: {ex.Message}", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Helper Methods

        private string ParseErrorMessage(string json)
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

        #endregion
    }
}
